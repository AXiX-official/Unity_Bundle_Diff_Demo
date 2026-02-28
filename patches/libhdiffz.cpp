#define LIBHDIFFZ_EXPORTS
#define _CompressPlugin_zlib  // memory requires less
#define _CompressPlugin_lzma2 // better compresser
#define _CompressPlugin_zstd  
#include "libhdiffz.h"

#include <vector>
#include <new>

// HDiffPatch headers
#include "libHDiffPatch/HDiff/match_block.h"
#include "libHDiffPatch/HDiff/diff.h"

// Compress/decompress plugins
#include "compress_plugin_demo.h"
#include "decompress_plugin_demo.h"

// ---- helpers ----

static const hdiff_TCompress* get_compress_plugin(hdiffz_compress_type type) {
    switch (type) {
#ifdef _CompressPlugin_zlib
        case HDIFFZ_COMPRESS_ZLIB:  return &zlibCompressPlugin.base;
#endif
#ifdef _CompressPlugin_zstd
        case HDIFFZ_COMPRESS_ZSTD:  return &zstdCompressPlugin.base;
#endif
#ifdef _CompressPlugin_lzma2
        case HDIFFZ_COMPRESS_LZMA2: return &lzma2CompressPlugin.base;
#endif
        case HDIFFZ_COMPRESS_NONE:
        default:
            return nullptr;
    }
}

static hpatch_TDecompress* s_decompress_plugins[] = {
#ifdef _CompressPlugin_zlib
    &zlibDecompressPlugin,
#endif
#ifdef _CompressPlugin_zstd
    &zstdDecompressPlugin,
#endif
#ifdef _CompressPlugin_lzma2
    &lzma2DecompressPlugin,
#endif
    nullptr
};

extern "C" {

LIBHDIFFZ_API hdiffz_result_t hdiffz_diff(
    const uint8_t* old_data, size_t old_size,
    const uint8_t* new_data, size_t new_size,
    hdiffz_compress_type compress_type,
    size_t match_block_size,
    size_t thread_num)
{
    auto* result = new(std::nothrow) std::vector<unsigned char>();
    if (!result) return nullptr;

    if (match_block_size == 0)
        match_block_size = kDefaultFastMatchBlockSize;
    if (thread_num == 0)
        thread_num = 1;

    const hdiff_TCompress* compressor = get_compress_plugin(compress_type);

    try {
        create_compressed_diff_block(
            const_cast<unsigned char*>(new_data),
            const_cast<unsigned char*>(new_data + new_size),
            const_cast<unsigned char*>(old_data),
            const_cast<unsigned char*>(old_data + old_size),
            *result,
            compressor,
            kMinSingleMatchScore_default,
            false,
            match_block_size,
            thread_num);
    } catch (...) {
        delete result;
        return nullptr;
    }

    return static_cast<hdiffz_result_t>(result);
}

LIBHDIFFZ_API const uint8_t* hdiffz_result_data(hdiffz_result_t result) {
    if (!result) return nullptr;
    return static_cast<std::vector<unsigned char>*>(result)->data();
}

LIBHDIFFZ_API size_t hdiffz_result_size(hdiffz_result_t result) {
    if (!result) return 0;
    return static_cast<std::vector<unsigned char>*>(result)->size();
}

LIBHDIFFZ_API void hdiffz_result_free(hdiffz_result_t result) {
    if (!result) return;
    delete static_cast<std::vector<unsigned char>*>(result);
}

LIBHDIFFZ_API int hdiffz_check(
    const uint8_t* old_data, size_t old_size,
    const uint8_t* new_data, size_t new_size,
    const uint8_t* diff_data, size_t diff_size)
{
    // Try each available decompressor; for uncompressed diffs, pass nullptr.
    for (int i = 0; s_decompress_plugins[i]; ++i) {
        try {
            if (check_compressed_diff(
                    new_data, new_data + new_size,
                    old_data, old_data + old_size,
                    diff_data, diff_data + diff_size,
                    s_decompress_plugins[i]))
                return 1;
        } catch (...) {}
    }
    // Try with no decompressor (uncompressed diff)
    try {
        if (check_compressed_diff(
                new_data, new_data + new_size,
                old_data, old_data + old_size,
                diff_data, diff_data + diff_size,
                nullptr))
            return 1;
    } catch (...) {}
    return 0;
}

} // extern "C"
