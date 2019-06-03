ImageTools
==========

A few image algorithms in C#. Nothing novel, just a place to put things.


## What?

* Q. Why should I use this instead of System.Drawing?
* A. Don't.

Really don't use this. These are a collection of algorithms for my own play and research.


## Plans and To-do

* [ ] Reduce memory footprint
* [ ] Experiment with non-linear quantisation
* [ ] Bring in the old bits from 'ImageCompress' (color cell compression) and delete that repo
* [ ] Try indexed entries for CDF coefficients (quantise by buckets)
* [ ] Attempt using 'wavelet tree' to encode CDF coefficients
* [ ] Attempt using the color cell algorithm for CDF coefficients