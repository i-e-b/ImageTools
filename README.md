ImageTools
==========

A few image algorithms in C#

## What?

* Q. Why should I use this instead of System.Drawing?
* A. Don't.

Really don't use this. These are a collection of algorithms for my own play and research.

## Contents

Experiments covering
* Color space mappings
* Compound/Planar format conversion
* Image compression/storage/decompression experiments
* Data stream interleaving
* Fibonacci encode/decode streams
* CDF and Haar wavelet tests
* Morton and Hilbert space filling curve reordering
* Box Blur / Soft-focus / sharpen
* Image scaling

## Plans and To-do

In no particular order:
* [x] Try image compression with Haar wavelet, to see how bad it is
    - Pretty bad. The compression ratio is good, but the quantisation artefacts are horrible.
* [x] Pixel-art style up-scaling
* [ ] Reduce memory footprint (think of a way to not hold an entire 3D image in memory twice...)
* [x] Experiment with non-linear quantisation
* [x] Find a good way to do non-power-two images
* [x] Bring in the old bits from 'ImageCompress' (color cell compression) and delete that repo
* [ ] Try indexed entries for CDF coefficients (quantise by buckets)
* [ ] Attempt using 'wavelet tree' to encode CDF coefficients
* [ ] Attempt using the color cell algorithm for CDF coefficients
* [ ] 3D-image compress a larger video and compare size (requires non-power-two 3D images)
* [ ] Move some of the encoding and lossless compression out to a separate project.
* [ ] Cleanup and optimise the fibonnacci encoding used in wavelet stuff
* [ ] Other universal coding (Elias / Golomb)
