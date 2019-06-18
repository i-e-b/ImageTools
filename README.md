ImageTools
==========

A few image algorithms in C#. Nothing novel, just a place to put things.


## What?

* Q. Why should I use this instead of System.Drawing?
* A. Don't.

Really don't use this. These are a collection of algorithms for my own play and research.


## Plans and To-do

In no particular order:
* [ ] Reduce memory footprint (think of a way to not hold an entire 3D image in memory twice...)
* [ ] Experiment with non-linear quantisation
* [x] Find a good way to do non-power-two images
* [ ] Bring in the old bits from 'ImageCompress' (color cell compression) and delete that repo
* [ ] Try indexed entries for CDF coefficients (quantise by buckets)
* [ ] Attempt using 'wavelet tree' to encode CDF coefficients
* [ ] Attempt using the color cell algorithm for CDF coefficients
* [ ] 3D-image compress a larger video and compare size (requires non-power-two images)
