#ifndef LIBHDIFFZ_H
#define LIBHDIFFZ_H

#include <stddef.h>
#include <stdint.h>

#ifdef _WIN32
#   ifdef LIBHDIFFZ_EXPORTS
#       define LIBHDIFFZ_API __declspec(dllexport)
#   else
#       define LIBHDIFFZ_API __declspec(dllimport)
#   endif
#else
#   define LIBHDIFFZ_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

typedef enum {
    HDIFFZ_COMPRESS_NONE  = 0,
    HDIFFZ_COMPRESS_ZLIB  = 1,
    HDIFFZ_COMPRESS_ZSTD  = 2,
    HDIFFZ_COMPRESS_LZMA2 = 3
} hdiffz_compress_type;

/* Opaque handle to a diff result (heap-allocated). */
typedef void* hdiffz_result_t;

/* Create a compressed block diff.
   Returns NULL on failure. */
LIBHDIFFZ_API hdiffz_result_t hdiffz_diff(
    const uint8_t* old_data, size_t old_size,
    const uint8_t* new_data, size_t new_size,
    hdiffz_compress_type compress_type,
    size_t match_block_size,
    size_t thread_num);

/* Access the diff result data pointer. */
LIBHDIFFZ_API const uint8_t* hdiffz_result_data(hdiffz_result_t result);

/* Get the diff result size in bytes. */
LIBHDIFFZ_API size_t hdiffz_result_size(hdiffz_result_t result);

/* Free the diff result. */
LIBHDIFFZ_API void hdiffz_result_free(hdiffz_result_t result);

/* Verify a compressed diff against old/new data.
   Returns 1 on success, 0 on failure. */
LIBHDIFFZ_API int hdiffz_check(
    const uint8_t* old_data, size_t old_size,
    const uint8_t* new_data, size_t new_size,
    const uint8_t* diff_data, size_t diff_size);

#ifdef __cplusplus
}
#endif

#endif /* LIBHDIFFZ_H */
