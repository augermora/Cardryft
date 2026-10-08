/* Copyright 2026 Cardryft contributors. MIT. Protocol facts are traced to the
 * pinned libusbmuxd/libimobiledevice sources; this is an independent narrow
 * implementation, not their unrestricted API or an Apple SDK. */
#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <limits.h>
#include <plist/plist.h>
#include <openssl/ssl.h>
#include <openssl/pem.h>
#include <openssl/crypto.h>
#include "cardryft_device.h"

#define CONTEXT_MAGIC 0x43464331u
#define SESSION_MAGIC 0x43465331u
struct CardryftContext {
    uint32_t magic;
    uint64_t deadline;
    uint32_t count;
    CardryftUsbDevice devices[CARDRYFT_MAX_DEVICES];
};
struct CardryftSession {
    uint32_t magic;
    SOCKET socket;
    uint64_t deadline;
    SSL_CTX *tls_context;
    SSL *tls;
};
_Static_assert(sizeof(CardryftUsbDevice)==136, "ABI device layout");
_Static_assert(sizeof(CardryftError)==4, "ABI error width");
_Static_assert(sizeof(CardryftMetadataField)==4, "ABI field width");

static uint64_t now_ms(void) { return GetTickCount64(); }
static uint64_t minimum(uint64_t a, uint64_t b) { return a<b?a:b; }
static const char *metadata_key(CardryftMetadataField field)
{
    switch(field) {
        case CARDRYFT_DEVICE_NAME: return "DeviceName";
        case CARDRYFT_PRODUCT_TYPE: return "ProductType";
        case CARDRYFT_PRODUCT_VERSION: return "ProductVersion";
        case CARDRYFT_BUILD_VERSION: return "BuildVersion";
        default: return NULL;
    }
}
static int transport_allowed(void)
{
    /* No environment address is ever used. Reject even an empty override. */
    SetLastError(ERROR_SUCCESS);
    DWORD length=GetEnvironmentVariableW(L"USBMUXD_SOCKET_ADDRESS",NULL,0);
    return length==0 && GetLastError()==ERROR_ENVVAR_NOT_FOUND;
}
static int identifier_valid(const char *text)
{
    if(!text) return 0;
    for(uint32_t i=0;i<=CARDRYFT_MAX_IDENTIFIER;i++) {
        unsigned char c=(unsigned char)text[i];
        if(!c) return i>0;
        if(!((c>='0'&&c<='9')||(c>='a'&&c<='z')||(c>='A'&&c<='Z')||c=='-')) return 0;
    }
    return 0;
}
static CardryftError wait_socket(SOCKET socket, int writing, uint64_t deadline)
{
    uint64_t now=now_ms();
    if(now>=deadline) return CARDRYFT_TIMEOUT;
    uint64_t remaining=deadline-now;
    struct timeval timeout={(long)(remaining/1000),(long)((remaining%1000)*1000)};
    fd_set ready; FD_ZERO(&ready); FD_SET(socket,&ready);
    int result=select(0,writing?NULL:&ready,writing?&ready:NULL,NULL,&timeout);
    return result>0?CARDRYFT_OK:result==0?CARDRYFT_TIMEOUT:CARDRYFT_TRANSPORT_UNAVAILABLE;
}
static CardryftError connect_local(SOCKET *out, uint64_t deadline)
{
    *out=INVALID_SOCKET;
    if(!transport_allowed()) return CARDRYFT_TRANSPORT_UNAVAILABLE;
    SOCKET s=socket(AF_INET,SOCK_STREAM,IPPROTO_TCP);
    if(s==INVALID_SOCKET) return CARDRYFT_TRANSPORT_UNAVAILABLE;
    u_long nonblocking=1;
    if(ioctlsocket(s,FIONBIO,&nonblocking)!=0) { closesocket(s); return CARDRYFT_TRANSPORT_UNAVAILABLE; }
    struct sockaddr_in address; memset(&address,0,sizeof(address));
    address.sin_family=AF_INET; address.sin_port=htons(27015);
    address.sin_addr.s_addr=htonl(0x7f000001u);
    CardryftError error=CARDRYFT_OK;
    if(connect(s,(struct sockaddr*)&address,sizeof(address))==SOCKET_ERROR) {
        int code=WSAGetLastError();
        if(code!=WSAEWOULDBLOCK && code!=WSAEINPROGRESS) error=CARDRYFT_TRANSPORT_UNAVAILABLE;
        else {
            error=wait_socket(s,1,deadline);
            int status=0,size=sizeof(status);
            if(error==CARDRYFT_OK && (getsockopt(s,SOL_SOCKET,SO_ERROR,(char*)&status,&size)!=0 || status)) error=CARDRYFT_TRANSPORT_UNAVAILABLE;
        }
    }
    if(error!=CARDRYFT_OK) { closesocket(s); return error; }
    *out=s; return CARDRYFT_OK;
}
static CardryftError socket_bytes(SOCKET s, unsigned char *bytes, uint32_t size, int writing, uint64_t deadline)
{
    if(size>CARDRYFT_MAX_FRAME) return CARDRYFT_LIMIT;
    uint32_t done=0;
    while(done<size) {
        CardryftError error=wait_socket(s,writing,deadline);
        if(error!=CARDRYFT_OK) return error;
        int count=writing?send(s,(const char*)bytes+done,(int)(size-done),0):recv(s,(char*)bytes+done,(int)(size-done),0);
        if(count==SOCKET_ERROR && WSAGetLastError()==WSAEWOULDBLOCK) continue;
        if(count<=0) return CARDRYFT_NO_DEVICE;
        done+=(uint32_t)count;
    }
    return CARDRYFT_OK;
}
static CardryftError parse_frame(const unsigned char *data, uint32_t size, plist_t *out)
{
    *out=NULL;
    if(!size || size>CARDRYFT_MAX_FRAME) return CARDRYFT_LIMIT;
    /* Explicit supported formats only; no JSON/OpenStep fallback. */
    plist_err_t result;
    if(size>=8 && !memcmp(data,"bplist00",8)) result=plist_from_bin((const char*)data,size,out);
    else if(size>=5 && !memcmp(data,"<?xml",5)) result=plist_from_xml((const char*)data,size,out);
    else return CARDRYFT_INVALID_RESPONSE;
    if(result!=PLIST_ERR_SUCCESS || !*out || plist_get_node_type(*out)!=PLIST_DICT) {
        plist_free(*out); *out=NULL; return CARDRYFT_INVALID_RESPONSE;
    }
    return CARDRYFT_OK;
}
static CardryftError string_value(plist_t node, const char **out, uint32_t maximum)
{
    *out=NULL;
    if(!node || plist_get_node_type(node)!=PLIST_STRING) return CARDRYFT_INVALID_RESPONSE;
    uint64_t length=0; const char *text=plist_get_string_ptr(node,&length);
    if(!text || length>maximum || memchr(text,0,(size_t)length)) return CARDRYFT_LIMIT;
    /* Strict UTF-8; conversion checks encoding without allocating a string. */
    if(length && !MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,text,(int)length,NULL,0)) return CARDRYFT_INVALID_RESPONSE;
    *out=text; return CARDRYFT_OK;
}
static int string_equals(plist_t dictionary, const char *key, const char *expected)
{
    const char *text=NULL;
    return string_value(plist_dict_get_item(dictionary,key),&text,128)==CARDRYFT_OK && !strcmp(text,expected);
}
static CardryftError mux_exchange(SOCKET s, const char *request, plist_t arguments, plist_t *out, uint64_t deadline)
{
    *out=NULL;
    plist_t message=arguments?plist_copy(arguments):plist_new_dict();
    if(!message) return CARDRYFT_NO_MEMORY;
    plist_dict_set_item(message,"MessageType",plist_new_string(request));
    plist_dict_set_item(message,"ClientVersionString",plist_new_string("Cardryft"));
    plist_dict_set_item(message,"ProgName",plist_new_string("Cardryft"));
    char *xml=NULL; uint32_t length=0;
    plist_err_t encoded=plist_to_xml(message,&xml,&length); plist_free(message);
    if(encoded!=PLIST_ERR_SUCCESS || !xml) return CARDRYFT_NO_MEMORY;
    if(!length || length>CARDRYFT_MAX_FRAME-16) { plist_mem_free(xml); return CARDRYFT_LIMIT; }
    uint32_t header[4]={length+16,1,8,1}; /* pinned usbmux v1, plist, tag 1; one request/socket */
    CardryftError error=socket_bytes(s,(unsigned char*)header,16,1,deadline);
    if(error==CARDRYFT_OK) error=socket_bytes(s,(unsigned char*)xml,length,1,deadline);
    plist_mem_free(xml);
    if(error==CARDRYFT_OK) error=socket_bytes(s,(unsigned char*)header,16,0,deadline);
    if(error!=CARDRYFT_OK) return error;
    if(header[0]<=16 || header[0]>CARDRYFT_MAX_FRAME || header[1]!=1 || header[2]!=8 || header[3]!=1) return CARDRYFT_INVALID_RESPONSE;
    length=header[0]-16;
    unsigned char *buffer=(unsigned char*)malloc(length);
    if(!buffer) return CARDRYFT_NO_MEMORY;
    error=socket_bytes(s,buffer,length,0,deadline);
    if(error==CARDRYFT_OK) error=parse_frame(buffer,length,out);
    OPENSSL_cleanse(buffer,length); free(buffer); return error;
}
static CardryftError mux_request(const char *request, plist_t arguments, plist_t *out, uint64_t deadline)
{
    SOCKET s; *out=NULL;
    CardryftError error=connect_local(&s,deadline);
    if(error==CARDRYFT_OK) { error=mux_exchange(s,request,arguments,out,deadline); closesocket(s); }
    return error;
}
static CardryftError tls_flush(CardryftSession *session)
{
    BIO *output=SSL_get_wbio(session->tls);
    unsigned char buffer[16384];
    size_t pending=BIO_ctrl_pending(output);
    if(pending>CARDRYFT_MAX_FRAME) return CARDRYFT_LIMIT;
    while(pending) {
        int count=BIO_read(output,buffer,sizeof(buffer));
        if(count<=0) return CARDRYFT_INVALID_RESPONSE;
        CardryftError error=socket_bytes(session->socket,buffer,(uint32_t)count,1,session->deadline);
        if(error!=CARDRYFT_OK) return error;
        pending=BIO_ctrl_pending(output);
    }
    return CARDRYFT_OK;
}
static CardryftError tls_input(CardryftSession *session)
{
    CardryftError error=tls_flush(session);
    if(error!=CARDRYFT_OK) return error;
    error=wait_socket(session->socket,0,session->deadline);
    if(error!=CARDRYFT_OK) return error;
    unsigned char buffer[16384]; int count=recv(session->socket,(char*)buffer,sizeof(buffer),0);
    if(count==SOCKET_ERROR && WSAGetLastError()==WSAEWOULDBLOCK) return CARDRYFT_OK;
    if(count<=0) return CARDRYFT_NO_DEVICE;
    BIO *input=SSL_get_rbio(session->tls);
    if(BIO_ctrl_pending(input)+(size_t)count>CARDRYFT_MAX_FRAME) return CARDRYFT_LIMIT;
    return BIO_write(input,buffer,count)==count?CARDRYFT_OK:CARDRYFT_NO_MEMORY;
}
static CardryftError session_bytes(CardryftSession *session, unsigned char *bytes, uint32_t size, int writing)
{
    if(!session->tls) return socket_bytes(session->socket,bytes,size,writing,session->deadline);
    uint32_t done=0;
    while(done<size) {
        if(now_ms()>=session->deadline) return CARDRYFT_TIMEOUT;
        size_t count=0;
        int result=writing?SSL_write_ex(session->tls,bytes+done,size-done,&count):SSL_read_ex(session->tls,bytes+done,size-done,&count);
        if(result==1) { if(!count || count>size-done) return CARDRYFT_INVALID_RESPONSE; done+=(uint32_t)count; }
        else {
            int code=SSL_get_error(session->tls,result);
            if(code!=SSL_ERROR_WANT_READ && code!=SSL_ERROR_WANT_WRITE) return CARDRYFT_NOT_TRUSTED;
            CardryftError error=tls_input(session); if(error!=CARDRYFT_OK) return error;
        }
        CardryftError error=tls_flush(session); if(error!=CARDRYFT_OK) return error;
    }
    return CARDRYFT_OK;
}
static CardryftError lockdown_request(CardryftSession *session, const char *request, plist_t arguments, plist_t *out)
{
    *out=NULL;
    plist_t message=arguments?plist_copy(arguments):plist_new_dict();
    if(!message) return CARDRYFT_NO_MEMORY;
    plist_dict_set_item(message,"Request",plist_new_string(request));
    plist_dict_set_item(message,"Label",plist_new_string("Cardryft"));
    char *xml=NULL; uint32_t length=0;
    plist_err_t encoded=plist_to_xml(message,&xml,&length); plist_free(message);
    if(encoded!=PLIST_ERR_SUCCESS || !xml) return CARDRYFT_NO_MEMORY;
    if(!length || length>CARDRYFT_MAX_FRAME) { plist_mem_free(xml); return CARDRYFT_LIMIT; }
    uint32_t prefix=htonl(length);
    CardryftError error=session_bytes(session,(unsigned char*)&prefix,4,1);
    if(error==CARDRYFT_OK) error=session_bytes(session,(unsigned char*)xml,length,1);
    plist_mem_free(xml);
    if(error==CARDRYFT_OK) error=session_bytes(session,(unsigned char*)&prefix,4,0);
    if(error!=CARDRYFT_OK) return error;
    length=ntohl(prefix);
    if(!length || length>CARDRYFT_MAX_FRAME) return CARDRYFT_LIMIT;
    unsigned char *buffer=(unsigned char*)malloc(length);
    if(!buffer) return CARDRYFT_NO_MEMORY;
    error=session_bytes(session,buffer,length,0);
    if(error==CARDRYFT_OK) error=parse_frame(buffer,length,out);
    OPENSSL_cleanse(buffer,length); free(buffer);
    if(error!=CARDRYFT_OK) return error;
    if(!string_equals(*out,"Request",request)) return CARDRYFT_INVALID_RESPONSE;
    plist_t failure=plist_dict_get_item(*out,"Error");
    if(failure) {
        const char *text=NULL; if(string_value(failure,&text,128)!=CARDRYFT_OK) return CARDRYFT_INVALID_RESPONSE;
        if(!strcmp(text,"PasswordProtected")||!strcmp(text,"GetProhibited")||!strcmp(text,"EscrowLocked")) return CARDRYFT_RESTRICTED;
        if(!strcmp(text,"InvalidHostID")||!strcmp(text,"MissingPairRecord")||!strcmp(text,"InvalidPairRecord")||!strcmp(text,"UserDeniedPairing")||!strcmp(text,"PairingDialogResponsePending")) return CARDRYFT_NOT_TRUSTED;
        return CARDRYFT_INVALID_RESPONSE;
    }
    return CARDRYFT_OK;
}
static X509 *record_certificate(plist_t record, const char *key)
{
    plist_t node=plist_dict_get_item(record,key);
    uint64_t size=0; const char *data=plist_get_node_type(node)==PLIST_DATA?plist_get_data_ptr(node,&size):NULL;
    if(!data || !size || size>16384) return NULL;
    BIO *bio=BIO_new_mem_buf(data,(int)size); if(!bio) return NULL;
    X509 *cert=PEM_read_bio_X509(bio,NULL,NULL,NULL); BIO_free(bio); return cert;
}
static SSL_CTX *trusted_tls_context(X509 *root, EVP_PKEY *private_key)
{
    SSL_CTX *context=SSL_CTX_new(TLS_client_method());
    if(!context) return NULL;
    SSL_CTX_set_security_level(context,2);
    SSL_CTX_set_options(context,SSL_OP_NO_RENEGOTIATION|SSL_OP_NO_COMPRESSION);
    SSL_CTX_set_max_cert_list(context,16384);
    SSL_CTX_set_verify(context,SSL_VERIFY_PEER,NULL);
    if(!SSL_CTX_set_min_proto_version(context,TLS1_2_VERSION) ||
       X509_STORE_add_cert(SSL_CTX_get_cert_store(context),root)!=1 ||
       SSL_CTX_use_certificate(context,root)!=1 ||
       SSL_CTX_use_PrivateKey(context,private_key)!=1 ||
       SSL_CTX_check_private_key(context)!=1) { SSL_CTX_free(context); return NULL; }
    return context;
}
static int authenticated_peer(SSL *tls, X509 *expected)
{
    X509 *peer=SSL_get1_peer_certificate(tls);
    int valid=peer && SSL_get_verify_result(tls)==X509_V_OK && X509_cmp(peer,expected)==0;
    X509_free(peer); return valid;
}
static CardryftError authenticate_tls(CardryftSession *session, plist_t record)
{
    CardryftError error=CARDRYFT_NOT_TRUSTED;
    X509 *root=record_certificate(record,"RootCertificate");
    X509 *expected=record_certificate(record,"DeviceCertificate");
    EVP_PKEY *private_key=NULL; BIO *key_bio=NULL;
    plist_t node=plist_dict_get_item(record,"RootPrivateKey");
    uint64_t size=0; const char *data=plist_get_node_type(node)==PLIST_DATA?plist_get_data_ptr(node,&size):NULL;
    if(!root || !expected || !data || !size || size>16384) goto cleanup;
    key_bio=BIO_new_mem_buf(data,(int)size); if(!key_bio) goto cleanup;
    private_key=PEM_read_bio_PrivateKey(key_bio,NULL,NULL,NULL);
    if(!private_key) goto cleanup;
    session->tls_context=trusted_tls_context(root,private_key); if(!session->tls_context) goto cleanup;
    session->tls=SSL_new(session->tls_context); if(!session->tls) goto cleanup;
    BIO *input=BIO_new(BIO_s_mem()), *output=BIO_new(BIO_s_mem());
    if(!input || !output) { BIO_free(input); BIO_free(output); goto cleanup; }
    SSL_set_bio(session->tls,input,output); SSL_set_connect_state(session->tls);
    while(now_ms()<session->deadline) {
        int result=SSL_do_handshake(session->tls);
        if(result==1) {
            error=authenticated_peer(session->tls,expected)?tls_flush(session):CARDRYFT_NOT_TRUSTED; goto cleanup;
        }
        int code=SSL_get_error(session->tls,result);
        if(code!=SSL_ERROR_WANT_READ && code!=SSL_ERROR_WANT_WRITE) goto cleanup;
        error=tls_input(session); if(error!=CARDRYFT_OK) goto cleanup;
        error=CARDRYFT_NOT_TRUSTED;
    }
    error=CARDRYFT_TIMEOUT;
cleanup:
    BIO_free(key_bio); EVP_PKEY_free(private_key); X509_free(root); X509_free(expected);
    return error;
}
uint32_t cardryft_abi_version(void) { return CARDRYFT_ABI_VERSION; }
CardryftError cardryft_initialize(CardryftContext **out)
{
    if(!out) return CARDRYFT_INVALID_ARGUMENT;
    *out=NULL;
    if(!transport_allowed()) return CARDRYFT_TRANSPORT_UNAVAILABLE;
    WSADATA data;
    if(WSAStartup(MAKEWORD(2,2),&data)!=0) return CARDRYFT_TRANSPORT_UNAVAILABLE;
    CardryftContext *context=(CardryftContext*)calloc(1,sizeof(*context));
    if(!context) { WSACleanup(); return CARDRYFT_NO_MEMORY; }
    context->magic=CONTEXT_MAGIC; *out=context; return CARDRYFT_OK;
}
CardryftError cardryft_enumerate_usb(CardryftContext *context, CardryftUsbDevice *devices, uint32_t capacity, uint32_t *count)
{
    if(!context || context->magic!=CONTEXT_MAGIC || !devices || !count || !capacity || capacity>CARDRYFT_MAX_DEVICES) return CARDRYFT_INVALID_ARGUMENT;
    *count=0; memset(devices,0,capacity*sizeof(*devices));
    context->count=0; memset(context->devices,0,sizeof(context->devices));
    context->deadline=now_ms()+CARDRYFT_REFRESH_MS;
    plist_t reply=NULL;
    CardryftError error=mux_request("ListDevices",NULL,&reply,minimum(context->deadline,now_ms()+CARDRYFT_ENUMERATION_MS));
    if(error!=CARDRYFT_OK) goto cleanup;
    plist_t list=plist_dict_get_item(reply,"DeviceList");
    if(plist_get_node_type(list)!=PLIST_ARRAY) { error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
    uint32_t length=plist_array_get_size(list);
    if(length>CARDRYFT_MAX_DEVICES) { error=CARDRYFT_LIMIT; goto cleanup; }
    for(uint32_t i=0;i<length;i++) {
        plist_t properties=plist_dict_get_item(plist_array_get_item(list,i),"Properties");
        if(plist_get_node_type(properties)!=PLIST_DICT) { error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
        if(string_equals(properties,"ConnectionType","Network")) continue;
        if(!string_equals(properties,"ConnectionType","USB")) { error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
        const char *identifier=NULL;
        error=string_value(plist_dict_get_item(properties,"SerialNumber"),&identifier,CARDRYFT_MAX_IDENTIFIER);
        if(error!=CARDRYFT_OK || !identifier_valid(identifier)) { error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
        plist_t id=plist_dict_get_item(properties,"DeviceID"); uint64_t usb_id=0;
        if(plist_get_node_type(id)!=PLIST_UINT) { error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
        plist_get_uint_val(id,&usb_id);
        if(!usb_id || usb_id>UINT32_MAX || context->count>=capacity) { error=CARDRYFT_LIMIT; goto cleanup; }
        for(uint32_t j=0;j<context->count;j++) if(context->devices[j].usb_id==(uint32_t)usb_id || !strcmp(context->devices[j].identifier,identifier)) { error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
        CardryftUsbDevice *item=&context->devices[context->count++];
        item->usb_id=(uint32_t)usb_id; memcpy(item->identifier,identifier,strlen(identifier)+1);
    }
    memcpy(devices,context->devices,context->count*sizeof(*devices)); *count=context->count;
cleanup:
    plist_free(reply);
    if(error!=CARDRYFT_OK) { context->count=0; memset(context->devices,0,sizeof(context->devices)); }
    return error;
}
CardryftError cardryft_open_existing_trust(CardryftContext *context, const char *identifier, CardryftSession **out)
{
    if(!out) return CARDRYFT_INVALID_ARGUMENT;
    *out=NULL;
    if(!context || context->magic!=CONTEXT_MAGIC || !identifier_valid(identifier)) return CARDRYFT_INVALID_ARGUMENT;
    uint32_t usb_id=0;
    for(uint32_t i=0;i<context->count;i++) if(!strcmp(context->devices[i].identifier,identifier)) usb_id=context->devices[i].usb_id;
    if(!usb_id) return CARDRYFT_NO_DEVICE;
    uint64_t deadline=minimum(context->deadline,now_ms()+CARDRYFT_DEVICE_MS);
    if(now_ms()>=deadline) return CARDRYFT_TIMEOUT;
    CardryftSession *session=(CardryftSession*)calloc(1,sizeof(*session));
    if(!session) return CARDRYFT_NO_MEMORY;
    session->magic=SESSION_MAGIC; session->socket=INVALID_SOCKET; session->deadline=deadline;
    plist_t arguments=plist_new_dict(), reply=NULL, record=NULL;
    plist_dict_set_item(arguments,"PairRecordID",plist_new_string(identifier));
    CardryftError error=mux_request("ReadPairRecord",arguments,&reply,deadline);
    plist_free(arguments); arguments=NULL;
    if(error!=CARDRYFT_OK) goto cleanup;
    plist_t blob=plist_dict_get_item(reply,"PairRecordData"); uint64_t size=0;
    const char *bytes=plist_get_node_type(blob)==PLIST_DATA?plist_get_data_ptr(blob,&size):NULL;
    if(!bytes || !size || size>CARDRYFT_MAX_FRAME) { error=CARDRYFT_NOT_TRUSTED; goto cleanup; }
    error=parse_frame((const unsigned char*)bytes,(uint32_t)size,&record);
    if(error!=CARDRYFT_OK) goto cleanup;
    const char *host_id=NULL;
    error=string_value(plist_dict_get_item(record,"HostID"),&host_id,128);
    if(error!=CARDRYFT_OK || !identifier_valid(host_id)) { error=CARDRYFT_NOT_TRUSTED; goto cleanup; }
    plist_free(reply); reply=NULL;
    error=mux_request("ReadBUID",NULL,&reply,deadline); if(error!=CARDRYFT_OK) goto cleanup;
    const char *buid=NULL;
    error=string_value(plist_dict_get_item(reply,"BUID"),&buid,128);
    if(error!=CARDRYFT_OK || !identifier_valid(buid)) { error=CARDRYFT_NOT_TRUSTED; goto cleanup; }
    arguments=plist_new_dict();
    plist_dict_set_item(arguments,"HostID",plist_new_string(host_id));
    plist_dict_set_item(arguments,"SystemBUID",plist_new_string(buid));
    plist_free(reply); reply=NULL;
    error=connect_local(&session->socket,deadline); if(error!=CARDRYFT_OK) goto cleanup;
    plist_t connect_arguments=plist_new_dict();
    plist_dict_set_item(connect_arguments,"DeviceID",plist_new_uint(usb_id));
    plist_dict_set_item(connect_arguments,"PortNumber",plist_new_uint(htons(62078)));
    error=mux_exchange(session->socket,"Connect",connect_arguments,&reply,deadline);
    plist_free(connect_arguments);
    if(error!=CARDRYFT_OK) goto cleanup;
    plist_t number=plist_dict_get_item(reply,"Number"); uint64_t code=UINT64_MAX;
    if(plist_get_node_type(number)==PLIST_UINT) plist_get_uint_val(number,&code);
    if(!string_equals(reply,"MessageType","Result") || code!=0) { error=CARDRYFT_NO_DEVICE; goto cleanup; }
    plist_free(reply); reply=NULL;
    error=lockdown_request(session,"QueryType",NULL,&reply);
    if(error!=CARDRYFT_OK || !string_equals(reply,"Type","com.apple.mobile.lockdown")) { if(error==CARDRYFT_OK) error=CARDRYFT_INVALID_RESPONSE; goto cleanup; }
    plist_free(reply); reply=NULL;
    error=lockdown_request(session,"StartSession",arguments,&reply); if(error!=CARDRYFT_OK) goto cleanup;
    uint8_t use_tls=0; plist_t enabled=plist_dict_get_item(reply,"EnableSessionSSL");
    if(plist_get_node_type(enabled)==PLIST_BOOLEAN) plist_get_bool_val(enabled,&use_tls);
    const char *session_id=NULL;
    if(!use_tls || string_value(plist_dict_get_item(reply,"SessionID"),&session_id,128)!=CARDRYFT_OK || !*session_id) { error=CARDRYFT_NOT_TRUSTED; goto cleanup; }
    error=authenticate_tls(session,record);
    if(error==CARDRYFT_OK) { *out=session; session=NULL; }
cleanup:
    plist_free(arguments); plist_free(reply); plist_free(record); cardryft_release(session); return error;
}
CardryftError cardryft_query_metadata(CardryftSession *session, CardryftMetadataField field, char *utf8, uint32_t capacity, uint32_t *length)
{
    if(!length || !utf8 || capacity<1 || capacity>CARDRYFT_MAX_STRING+1) return CARDRYFT_INVALID_ARGUMENT;
    *length=0; memset(utf8,0,capacity);
    const char *key=metadata_key(field);
    if(!key || !session || session->magic!=SESSION_MAGIC || !session->tls) return CARDRYFT_INVALID_ARGUMENT;
    if(!transport_allowed()) return CARDRYFT_TRANSPORT_UNAVAILABLE;
    if(now_ms()>=session->deadline) return CARDRYFT_TIMEOUT;
    plist_t arguments=plist_new_dict(), reply=NULL;
    plist_dict_set_item(arguments,"Key",plist_new_string(key));
    CardryftError error=lockdown_request(session,"GetValue",arguments,&reply);
    plist_free(arguments);
    if(error==CARDRYFT_OK) {
        const char *text=NULL;
        error=string_value(plist_dict_get_item(reply,"Value"),&text,CARDRYFT_MAX_STRING);
        if(error==CARDRYFT_OK) {
            size_t count=strlen(text);
            if(count>=capacity || MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,text,(int)count,NULL,0)>256) error=CARDRYFT_LIMIT;
            else { memcpy(utf8,text,count+1); *length=(uint32_t)count; }
        }
    }
    plist_free(reply); return error;
}
void cardryft_release(void *owner)
{
    if(!owner) return;
    uint32_t magic=*(uint32_t*)owner;
    if(magic==CONTEXT_MAGIC) { OPENSSL_cleanse(owner,sizeof(CardryftContext)); free(owner); WSACleanup(); }
    else if(magic==SESSION_MAGIC) {
        CardryftSession *session=(CardryftSession*)owner;
        /* Pure local release: no StopSession, SSL_shutdown, send, recv, or wait. */
        SSL_free(session->tls); SSL_CTX_free(session->tls_context);
        if(session->socket!=INVALID_SOCKET) closesocket(session->socket);
        OPENSSL_cleanse(session,sizeof(*session)); free(session);
    }
}
