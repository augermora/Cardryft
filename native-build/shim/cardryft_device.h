/* Cardryft native boundary. Copyright 2026 Cardryft contributors. MIT. */
#ifndef CARDRYFT_DEVICE_H
#define CARDRYFT_DEVICE_H
#include <stdint.h>
#define CARDRYFT_ABI_VERSION 0x00010000u
#define CARDRYFT_MAX_DEVICES 32u
#define CARDRYFT_MAX_IDENTIFIER 128u
#define CARDRYFT_MAX_STRING 1024u
#define CARDRYFT_MAX_FRAME 65536u
#define CARDRYFT_ENUMERATION_MS 5000u
#define CARDRYFT_DEVICE_MS 10000u
#define CARDRYFT_REFRESH_MS 30000u
typedef struct CardryftContext CardryftContext;
typedef struct CardryftSession CardryftSession;
typedef struct { uint32_t usb_id; char identifier[129]; } CardryftUsbDevice;
typedef enum {
    CARDRYFT_OK=0, CARDRYFT_INVALID_ARGUMENT=1, CARDRYFT_TRANSPORT_UNAVAILABLE=2,
    CARDRYFT_NOT_TRUSTED=3, CARDRYFT_RESTRICTED=4, CARDRYFT_INVALID_RESPONSE=5,
    CARDRYFT_LIMIT=6, CARDRYFT_TIMEOUT=7, CARDRYFT_NO_DEVICE=8, CARDRYFT_NO_MEMORY=9
} CardryftError;
typedef enum {
    CARDRYFT_DEVICE_NAME=0, CARDRYFT_PRODUCT_TYPE=1,
    CARDRYFT_PRODUCT_VERSION=2, CARDRYFT_BUILD_VERSION=3
} CardryftMetadataField;
uint32_t cardryft_abi_version(void);
CardryftError cardryft_initialize(CardryftContext **context);
CardryftError cardryft_enumerate_usb(CardryftContext *context, CardryftUsbDevice *devices, uint32_t capacity, uint32_t *count);
CardryftError cardryft_open_existing_trust(CardryftContext *context, const char *identifier, CardryftSession **session);
CardryftError cardryft_query_metadata(CardryftSession *session, CardryftMetadataField field, char *utf8, uint32_t capacity, uint32_t *length);
void cardryft_release(void *owner);
#endif
