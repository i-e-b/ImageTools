namespace ImageTools.DataCompression
{
    public class Lzjb
    {
        private const int NBBY = 8;
        private const int MATCH_BITS = 6;
        private const int MATCH_MIN = 3;
        private const int MATCH_MAX = ((1 << MATCH_BITS) + (MATCH_MIN - 1));
        private const int OFFSET_MASK = ((1 << (16 - MATCH_BITS)) - 1);
        private const int LEMPEL_SIZE = 256;

        /// <summary>
        /// Compress byte array using fast and efficient algorithm.
        /// </summary>
        /// <param name="sstart">The buffer to compress</param>
        /// <param name="dstart">The buffer to write into</param>
        /// <returns>compressed length (number of bytes written to the output buffer).
        /// May be bigger than the size of the output buffer, in which case some bytes are lost</returns>
        public static int Compress(byte[] sstart, byte[] dstart)
        {
            int slen, cpy, mlen, offset, i, hp,
                src = 0,
                dst = 0,
                copymap = 0,
                copymask = 1 << (NBBY - 1);
            
            var lempel = new int[LEMPEL_SIZE];

            // Initialize Lempel array.
            for(i = 0; i < LEMPEL_SIZE; i++)
            {
                lempel[i] = -858993460; // 0xCC_CC_CC_CC
            }

            slen = sstart.Length;

            while (src < slen)
            {
                if ((copymask <<= 1) == (1 << NBBY)) {
                    copymask = 1;
                    copymap = dst;
                    dstart[dst++] = 0;
                }

                if (src > slen - MATCH_MAX) {
                    dstart[dst++] = sstart[src++];
                    continue;
                }

                hp = ((sstart[src] + 13) ^
                      (sstart[src + 1] - 13) ^
                      sstart[src + 2]) &
                     (LEMPEL_SIZE - 1);

                offset = (src - lempel[hp]) & OFFSET_MASK;
                lempel[hp] = src;
                cpy = src - offset;

                if (cpy >= 0 && cpy != src &&
                    sstart[src] == sstart[cpy] &&
                    sstart[src + 1] == sstart[cpy + 1] &&
                    sstart[src + 2] == sstart[cpy + 2]) {
                    dstart[copymap] = (byte)(dstart[copymap] | copymask);
                    for (mlen = MATCH_MIN; mlen < MATCH_MAX; mlen++)
                        if (sstart[src + mlen] != sstart[cpy + mlen])
                            break;
                    dstart[dst++] = (byte)(((mlen - MATCH_MIN) << (NBBY - MATCH_BITS)) |
                                    (offset >> NBBY));
                    dstart[dst++] = (byte)offset;
                    src += mlen;
                } else {
                    dstart[dst++] = sstart[src++];
                }
            }

            Console.WriteLine($"sstart.length >= src  :  {sstart.Length >= src}");

            return dst;
        }

        /// <summary>
        /// Decompress byte array using fast and efficient algorithm.
        /// </summary>
        /// <param name="sstart">The buffer to decompress</param>
        /// <param name="slen">compressed length</param>
        /// <param name="dstart">The buffer to write into</param>
        /// <returns>decompressed length</returns>
        public static int Decompress(byte[] sstart, int slen, byte[] dstart)
        {
            int
                src = 0,
                dst = 0,
                cpy = 0,
                copymap = 0,
                copymask = 1 << (NBBY - 1 | 0),
                mlen = 0,
                offset = 0;

            //var avg_mlen = [];

            while (src < slen)
            {
                if ((copymask <<= 1) == (1 << NBBY))
                {
                    copymask = 1;
                    copymap = sstart[src];
                    src = src + 1 | 0;
                }

                if ((copymap & copymask) != 0)
                {
                    mlen = (sstart[src] >> (NBBY - MATCH_BITS | 0)) + MATCH_MIN | 0;
                    offset = ((sstart[src] << NBBY) | sstart[src + 1 | 0]) & OFFSET_MASK;
                    src = src + 2 | 0;

                    cpy = dst - offset | 0;
                    //if (cpy >= 0)
                    {
                        //console.log(mlen);
                        //avg_mlen.push(mlen);

                        //dstart.set(dstart.subarray(cpy, cpy + mlen | 0), dst);
                        //dst = dst + mlen | 0;
                        //cpy = cpy + mlen | 0;

                        //mlen = mlen - 1 | 0;
                        while (mlen > 4)
                        {
                            dstart[dst] = dstart[cpy];
                            dst = dst + 1 | 0;
                            cpy = cpy + 1 | 0;

                            dstart[dst] = dstart[cpy];
                            dst = dst + 1 | 0;
                            cpy = cpy + 1 | 0;

                            dstart[dst] = dstart[cpy];
                            dst = dst + 1 | 0;
                            cpy = cpy + 1 | 0;

                            dstart[dst] = dstart[cpy];
                            dst = dst + 1 | 0;
                            cpy = cpy + 1 | 0;

                            mlen = mlen - 4 | 0;
                        }

                        while (mlen > 0)
                        {
                            dstart[dst] = dstart[cpy];
                            dst = dst + 1 | 0;
                            cpy = cpy + 1 | 0;
                            mlen = mlen - 1 | 0;
                        }
                    }
                    //else
                    //{
                    //    /*
                    //     * offset before start of destination buffer
                    //     * indicates corrupt source data
                    //     * /
                    //    console.warn("possibly corrupt data");
                    //    return dstart;
                    //}
                }
                else
                {
                    dstart[dst] = sstart[src];
                    dst = dst + 1 | 0;
                    src = src + 1 | 0;
                }
            }

            //console.log(avg_mlen.reduce(function(a, x) { return a + x; }, 0) / avg_mlen.length);

            //console.assert(dstart.length >= dst);
            //console.assert(sstart.length >= src);

            return dst;
        }

        // Javascript source from https://github.com/copy/jslzjb-k/blob/master/lzjb.js
        /*
         /*
Based on jslzjb: https://code.google.com/p/jslzjb/
Heavily modified for speed
* /

var jslzjb = (function() {

    // Constants was used for compress/decompress function.
    var
        /** @const * / NBBY = 8,
        /** @const * / MATCH_BITS = 6,
        /** @const * / MATCH_MIN = 3,
        /** @const * / MATCH_MAX = ((1 << MATCH_BITS) + (MATCH_MIN - 1)),
        /** @const * / OFFSET_MASK = ((1 << (16 - MATCH_BITS)) - 1),
        /** @const * / LEMPEL_SIZE = 256;

    /**
     * Because of weak of javascript's natural, many compression algorithm
     * become useless in javascript implementation. The main problem is
     * performance, even the simple Huffman, LZ77/78 algorithm will take many
     * many time to operate. We use LZJB algorithm to do that, it suprisingly
     * fulfills our requirement to compress string fastly and efficiently.
     *
     * Our implementation is based on
     * http://src.opensolaris.org/source/raw/onnv/onnv-gate/
     * usr/src/uts/common/os/compress.c
     * It is licensed under CDDL.
     *
     * Compress byte array using fast and efficient algorithm.
     *
     * @param {Uint8Array} sstart  The buffer to compress
     * @param {Uint8Array} dstart  The buffer to write into
     * @return {number} compressed length (number of bytes written to the
     *                  output buffer). May be bigger than the size of the
     *                  output buffer, in which case some bytes are lost
     * /
    function compress(sstart, dstart)
    {
        var
            slen = 0,
            src = 0,
            dst = 0,
            cpy = 0,
            copymap = 0,
            copymask = 1 << (NBBY - 1),
            mlen = 0,
            offset = 0,
            hp = 0,
            lempel = new Int32Array(LEMPEL_SIZE),
            i = 0;

        // Initialize lempel array.
        for(i = 0; i < LEMPEL_SIZE; i++)
        {
            lempel[i] = -858993460;
        }

        slen = sstart.length;

        while (src < slen)
        {
            if ((copymask <<= 1) == (1 << NBBY)) {
                copymask = 1;
                copymap = dst;
                dstart[dst++] = 0;
            }

            if (src > slen - MATCH_MAX) {
                dstart[dst++] = sstart[src++];
                continue;
            }

            hp = ((sstart[src] + 13) ^
                  (sstart[src + 1] - 13) ^
                   sstart[src + 2]) &
                 (LEMPEL_SIZE - 1);

            offset = (src - lempel[hp]) & OFFSET_MASK;
            lempel[hp] = src;
            cpy = src - offset;

            if (cpy >= 0 && cpy != src &&
                sstart[src] == sstart[cpy] &&
                sstart[src + 1] == sstart[cpy + 1] &&
                sstart[src + 2] == sstart[cpy + 2]) {
                dstart[copymap] |= copymask;
                for (mlen = MATCH_MIN; mlen < MATCH_MAX; mlen++)
                    if (sstart[src + mlen] != sstart[cpy + mlen])
                        break;
                dstart[dst++] = ((mlen - MATCH_MIN) << (NBBY - MATCH_BITS)) |
                                (offset >> NBBY);
                dstart[dst++] = offset;
                src += mlen;
            } else {
                dstart[dst++] = sstart[src++];
            }
        }

        console.assert(sstart.length >= src);

        return dst;
    }

    /**
     * Our implementation is based on
     * http://src.opensolaris.org/source/raw/onnv/onnv-gate/
     * usr/src/uts/common/os/compress.c
     * It is licensed under CDDL.
     *
     * Decompress byte array using fast and efficient algorithm.
     *
     * @param {Uint8Array} sstart  The buffer to decompress
     * @param {number} slen  compressed length
     * @param {Uint8Array} dstart  The buffer to write into
     * @return {number} decompressed length
     * /
    function decompress(sstart, slen, dstart)
    {
        slen = slen | 0;

        var
            src = 0,
            dst = 0,
            cpy = 0,
            copymap = 0,
            copymask = 1 << (NBBY - 1 | 0),
            mlen = 0,
            offset = 0;

        //var avg_mlen = [];

        while (src < slen)
        {
            if ((copymask <<= 1) === (1 << NBBY))
            {
                copymask = 1;
                copymap = sstart[src];
                src = src + 1 | 0;
            }

            if (copymap & copymask)
            {
                mlen = (sstart[src] >> (NBBY - MATCH_BITS | 0)) + MATCH_MIN | 0;
                offset = ((sstart[src] << NBBY) | sstart[src + 1 | 0]) & OFFSET_MASK;
                src = src + 2 | 0;

                cpy = dst - offset | 0;
                //if (cpy >= 0)
                {
                    //console.log(mlen);
                    //avg_mlen.push(mlen);

                    //dstart.set(dstart.subarray(cpy, cpy + mlen | 0), dst);
                    //dst = dst + mlen | 0;
                    //cpy = cpy + mlen | 0;

                    //mlen = mlen - 1 | 0;
                    while (mlen > 4)
                    {
                        dstart[dst] = dstart[cpy];
                        dst = dst + 1 | 0;
                        cpy = cpy + 1 | 0;

                        dstart[dst] = dstart[cpy];
                        dst = dst + 1 | 0;
                        cpy = cpy + 1 | 0;

                        dstart[dst] = dstart[cpy];
                        dst = dst + 1 | 0;
                        cpy = cpy + 1 | 0;

                        dstart[dst] = dstart[cpy];
                        dst = dst + 1 | 0;
                        cpy = cpy + 1 | 0;

                        mlen = mlen - 4 | 0;
                    }

                    while (mlen > 0)
                    {
                        dstart[dst] = dstart[cpy];
                        dst = dst + 1 | 0;
                        cpy = cpy + 1 | 0;
                        mlen = mlen - 1 | 0;
                    }
                }
                //else
                //{
                //    /*
                //     * offset before start of destination buffer
                //     * indicates corrupt source data
                //     * /
                //    console.warn("possibly corrupt data");
                //    return dstart;
                //}
            }
            else
            {
                dstart[dst] = sstart[src];
                dst = dst + 1 | 0;
                src = src + 1 | 0;
            }
        }

        //console.log(avg_mlen.reduce(function(a, x) { return a + x; }, 0) / avg_mlen.length);

        //console.assert(dstart.length >= dst);
        //console.assert(sstart.length >= src);

        return dst;
    }

    return {
        compress: compress,
        decompress: decompress,
    };

})();

typeof module === "undefined" || (module.exports = jslzjb);
         */

        // Original C source:
        /*
     48 size_t
     49 lzjb_compress(void *s_start, void *d_start, size_t s_len, size_t d_len, int n)
     50 {
     51 	uchar_t *src = s_start;
     52 	uchar_t *dst = d_start;
     53 	uchar_t *cpy, *copymap;
     54 	int copymask = 1 << (NBBY - 1);
     55 	int mlen, offset, hash;
     56 	uint16_t *hp;
     57 	uint16_t lempel[LEMPEL_SIZE] = { 0 };
     58
     59 	while (src < (uchar_t *)s_start + s_len) {
     60 		if ((copymask <<= 1) == (1 << NBBY)) {
     61 			if (dst >= (uchar_t *)d_start + d_len - 1 - 2 * NBBY)
     62 				return (s_len);
     63 			copymask = 1;
     64 			copymap = dst;
     65 			*dst++ = 0;
     66 		}
     67 		if (src > (uchar_t *)s_start + s_len - MATCH_MAX) {
     68 			*dst++ = *src++;
     69 			continue;
     70 		}
     71 		hash = (src[0] << 16) + (src[1] << 8) + src[2];
     72 		hash += hash >> 9;
     73 		hash += hash >> 5;
     74 		hp = &lempel[hash & (LEMPEL_SIZE - 1)];
     75 		offset = (intptr_t)(src - *hp) & OFFSET_MASK;
     76 		*hp = (uint16_t)(uintptr_t)src;
     77 		cpy = src - offset;
     78 		if (cpy >= (uchar_t *)s_start && cpy != src &&
     79 		    src[0] == cpy[0] && src[1] == cpy[1] && src[2] == cpy[2]) {
     80 			*copymap |= copymask;
     81 			for (mlen = MATCH_MIN; mlen < MATCH_MAX; mlen++)
     82 				if (src[mlen] != cpy[mlen])
     83 					break;
     84 			*dst++ = ((mlen - MATCH_MIN) << (NBBY - MATCH_BITS)) |
     85 			    (offset >> NBBY);
     86 			*dst++ = (uchar_t)offset;
     87 			src += mlen;
     88 		} else {
     89 			*dst++ = *src++;
     90 		}
     91 	}
     92 	return (dst - (uchar_t *)d_start);
     93 }
     94
     95
     96 int
     97 lzjb_decompress(void *s_start, void *d_start, size_t s_len, size_t d_len, int n)
     98 {
     99 	uchar_t *src = s_start;
    100 	uchar_t *dst = d_start;
    101 	uchar_t *d_end = (uchar_t *)d_start + d_len;
    102 	uchar_t *cpy, copymap;
    103 	int copymask = 1 << (NBBY - 1);
    104
    105 	while (dst < d_end) {
    106 		if ((copymask <<= 1) == (1 << NBBY)) {
    107 			copymask = 1;
    108 			copymap = *src++;
    109 		}
    110 		if (copymap & copymask) {
    111 			int mlen = (src[0] >> (NBBY - MATCH_BITS)) + MATCH_MIN;
    112 			int offset = ((src[0] << NBBY) | src[1]) & OFFSET_MASK;
    113 			src += 2;
    114 			if ((cpy = dst - offset) < (uchar_t *)d_start)
    115 				return (-1);
    116 			while (--mlen >= 0 && dst < d_end)
    117 				*dst++ = *cpy++;
    118 		} else {
    119 			*dst++ = *src++;
    120 		}
    121 	}
    122 	return (0);
    123 }
    */
    }
}