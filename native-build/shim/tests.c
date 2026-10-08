/* Source-owned offline fixtures. Copyright 2026 Cardryft contributors. MIT.
 * Never invoke native enumeration/open/query or any socket function. */
#include <stdio.h>
#include <openssl/x509v3.h>
#include <openssl/err.h>
#include "cardryft_device.c"
static unsigned tests;
#define CHECK(condition) do { if(!(condition)) { fprintf(stderr,"FAIL %s:%d: %s\n",__FILE__,__LINE__,#condition); exit(1); } tests++; } while(0)
static X509 *certificate(EVP_PKEY *key, X509 *issuer, EVP_PKEY *signer, long serial, int ca)
{
    X509 *cert=X509_new(); if(!cert) exit(1);
    X509_set_version(cert,2); ASN1_INTEGER_set(X509_get_serialNumber(cert),serial);
    X509_gmtime_adj(X509_getm_notBefore(cert),-60); X509_gmtime_adj(X509_getm_notAfter(cert),86400);
    X509_set_pubkey(cert,key);
    X509_NAME *name=X509_get_subject_name(cert);
    char subject[64]; snprintf(subject,sizeof(subject),"Cardryft synthetic fixture %ld",serial);
    X509_NAME_add_entry_by_txt(name,"CN",MBSTRING_ASC,(const unsigned char*)subject,-1,-1,0);
    X509_set_issuer_name(cert,issuer?X509_get_subject_name(issuer):name);
    X509V3_CTX context; X509V3_set_ctx(&context,issuer?issuer:cert,cert,NULL,NULL,0);
    X509_EXTENSION *extension=X509V3_EXT_conf_nid(NULL,&context,NID_basic_constraints,ca?"critical,CA:TRUE":"critical,CA:FALSE");
    if(!extension) exit(1);
    X509_add_ext(cert,extension,-1); X509_EXTENSION_free(extension);
    if(!X509_sign(cert,signer?signer:key,EVP_sha256())) exit(1);
    return cert;
}
static void transfer(BIO *from, BIO *to)
{
    unsigned char bytes[16384]; int count;
    while((count=BIO_read(from,bytes,sizeof(bytes)))>0) if(BIO_write(to,bytes,count)!=count) exit(1);
}
static int handshake(SSL_CTX *client_context, X509 *server_cert, EVP_PKEY *server_key, X509 *expected)
{
    SSL_CTX *server_context=SSL_CTX_new(TLS_server_method());
    if(!server_context) exit(1);
    SSL_CTX_set_min_proto_version(server_context,TLS1_2_VERSION);
    if(SSL_CTX_use_certificate(server_context,server_cert)!=1 || SSL_CTX_use_PrivateKey(server_context,server_key)!=1) exit(1);
    SSL *client=SSL_new(client_context), *server=SSL_new(server_context);
    if(!client || !server) exit(1);
    BIO *ci=BIO_new(BIO_s_mem()),*co=BIO_new(BIO_s_mem()),*si=BIO_new(BIO_s_mem()),*so=BIO_new(BIO_s_mem());
    if(!ci||!co||!si||!so) exit(1);
    SSL_set_bio(client,ci,co); SSL_set_bio(server,si,so);
    SSL_set_connect_state(client); SSL_set_accept_state(server);
    int valid=0;
    for(unsigned i=0;i<32;i++) {
        int cr=SSL_do_handshake(client); int ce=cr==1?0:SSL_get_error(client,cr); transfer(co,si);
        int sr=SSL_do_handshake(server); int se=sr==1?0:SSL_get_error(server,sr); transfer(so,ci);
        if(cr==1 && sr==1) { valid=authenticated_peer(client,expected); break; }
        if(cr!=1 && ce!=SSL_ERROR_WANT_READ && ce!=SSL_ERROR_WANT_WRITE) break;
        if(sr!=1 && se!=SSL_ERROR_WANT_READ && se!=SSL_ERROR_WANT_WRITE) break;
    }
    SSL_free(client); SSL_free(server); SSL_CTX_free(server_context); ERR_clear_error(); return valid;
}
int main(void)
{
    CHECK(cardryft_abi_version()==CARDRYFT_ABI_VERSION);
    CHECK(sizeof(CardryftUsbDevice)==136);
    CHECK(sizeof(void*)==8);
    CHECK(metadata_key(CARDRYFT_DEVICE_NAME)&&!strcmp(metadata_key(CARDRYFT_DEVICE_NAME),"DeviceName"));
    CHECK(metadata_key(CARDRYFT_PRODUCT_TYPE)&&!strcmp(metadata_key(CARDRYFT_PRODUCT_TYPE),"ProductType"));
    CHECK(metadata_key(CARDRYFT_PRODUCT_VERSION)&&!strcmp(metadata_key(CARDRYFT_PRODUCT_VERSION),"ProductVersion"));
    CHECK(metadata_key(CARDRYFT_BUILD_VERSION)&&!strcmp(metadata_key(CARDRYFT_BUILD_VERSION),"BuildVersion"));
    CHECK(metadata_key((CardryftMetadataField)4)==NULL);
    CHECK(metadata_key((CardryftMetadataField)-1)==NULL);
    CHECK(identifier_valid("synthetic-001"));
    CHECK(!identifier_valid("")); CHECK(!identifier_valid("bad/path"));
    char identifier[130]; memset(identifier,'a',129); identifier[129]=0;
    CHECK(!identifier_valid(identifier)); identifier[128]=0; CHECK(identifier_valid(identifier));
    plist_t value=plist_new_string("synthetic"), result=NULL; const char *text=NULL;
    CHECK(string_value(value,&text,9)==CARDRYFT_OK); CHECK(string_value(value,&text,8)==CARDRYFT_LIMIT); plist_free(value);
    value=plist_new_string("\xc0\xaf"); CHECK(string_value(value,&text,10)==CARDRYFT_INVALID_RESPONSE); plist_free(value);
    value=plist_new_dict(); CHECK(string_value(value,&text,10)==CARDRYFT_INVALID_RESPONSE); plist_free(value);
    CHECK(parse_frame((const unsigned char*)"bad",3,&result)==CARDRYFT_INVALID_RESPONSE);
    CHECK(parse_frame((const unsigned char*)"x",65537,&result)==CARDRYFT_LIMIT);
    const char xml[]="<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>Value</key><string>fixture</string></dict></plist>";
    CHECK(parse_frame((const unsigned char*)xml,sizeof(xml)-1,&result)==CARDRYFT_OK); plist_free(result);
    char oversized[65537]; memset(oversized,'a',sizeof(oversized));
    CHECK(plist_from_xml(oversized,sizeof(oversized),&result)!=PLIST_ERR_SUCCESS); CHECK(result==NULL);
    char deep[4096]="<?xml version=\"1.0\"?><plist version=\"1.0\">";
    for(unsigned i=0;i<17;i++) strcat(deep,"<array>");
    strcat(deep,"<string>x</string>");
    for(unsigned i=0;i<17;i++) strcat(deep,"</array>");
    strcat(deep,"</plist>");
    CHECK(plist_from_xml(deep,(uint32_t)strlen(deep),&result)!=PLIST_ERR_SUCCESS); CHECK(result==NULL);
    char many[32768]="<?xml version=\"1.0\"?><plist version=\"1.0\"><array>";
    for(unsigned i=0;i<1025;i++) strcat(many,"<string>x</string>");
    strcat(many,"</array></plist>");
    CHECK(plist_from_xml(many,(uint32_t)strlen(many),&result)!=PLIST_ERR_SUCCESS); CHECK(result==NULL);
    value=plist_new_array(); plist_t root=value;
    for(unsigned i=0;i<17;i++) { plist_t child=plist_new_array(); plist_array_append_item(value,child); value=child; }
    plist_array_append_item(value,plist_new_string("x"));
    char *binary=NULL; uint32_t binary_size=0;
    CHECK(plist_to_bin(root,&binary,&binary_size)==PLIST_ERR_SUCCESS); plist_free(root);
    CHECK(plist_from_bin(binary,binary_size,&result)!=PLIST_ERR_SUCCESS); CHECK(result==NULL); plist_mem_free(binary);
    CHECK(cardryft_initialize(NULL)==CARDRYFT_INVALID_ARGUMENT);
    CHECK(cardryft_enumerate_usb(NULL,NULL,0,NULL)==CARDRYFT_INVALID_ARGUMENT);
    CHECK(cardryft_open_existing_trust(NULL,"fixture",NULL)==CARDRYFT_INVALID_ARGUMENT);
    char output[1025]; uint32_t length=99;
    CHECK(cardryft_query_metadata(NULL,(CardryftMetadataField)4,output,sizeof(output),&length)==CARDRYFT_INVALID_ARGUMENT);
    CHECK(length==0 && output[0]==0);
    CHECK(wait_socket(INVALID_SOCKET,0,now_ms())==CARDRYFT_TIMEOUT);
    cardryft_release(NULL);
    EVP_PKEY *root_key=EVP_RSA_gen(2048),*device_key=EVP_RSA_gen(2048),*other_key=EVP_RSA_gen(2048);
    if(!root_key||!device_key||!other_key) exit(1);
    X509 *root_cert=certificate(root_key,NULL,NULL,1,1);
    X509 *device_cert=certificate(device_key,root_cert,root_key,2,0);
    X509 *other_cert=certificate(other_key,NULL,NULL,3,1);
    SSL_CTX *context=trusted_tls_context(root_cert,root_key); CHECK(context!=NULL);
    CHECK(SSL_CTX_get_verify_mode(context)==SSL_VERIFY_PEER);
    CHECK(SSL_CTX_get_security_level(context)==2);
    CHECK(SSL_CTX_get_min_proto_version(context)==TLS1_2_VERSION);
    CHECK(handshake(context,device_cert,device_key,device_cert));
    CHECK(!handshake(context,device_cert,device_key,other_cert));
    CHECK(!handshake(context,other_cert,other_key,other_cert));
    SSL_CTX_free(context); X509_free(root_cert); X509_free(device_cert); X509_free(other_cert);
    EVP_PKEY_free(root_key); EVP_PKEY_free(device_key); EVP_PKEY_free(other_key);
    printf("Native offline fixtures: %u passed, 0 failed. No socket/device operations invoked.\n",tests);
    return 0;
}
