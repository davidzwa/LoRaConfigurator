Device A2 was flashed source
Device A0 was receiver

Did not save flash blob of 200f_14g, instead I exported it from STM32Programmer (64kB was sufficient).
rlnc_200f_10b_fake_14g_15r_1409709959rng_notused.bin is therefore marked `_notused`

Observations:
11/04 I notice bad performance for 10g, also I see that beyond PER 0.5 there is almost no use in measuring
11/04 Ok I found that beyond gen 12 we get consistent failures: this does not make any sense as those generations should succeed given the Update messages saying 17 dropped vs 23 received for 20/21 required.

Conclusion:
Have to dig for runtime bugs for gen12+ or PRNG issues.... Fixed!

Conclusion 12/04:
I found a bug in C#: it would register results with failure as permanent failure. I will stop sending Results altogether and depend on updates purely.

12/04 22:33 300f run5 success rate with 100% red max shows breakoff beyond ~30% PER (incl.) and semi-nice downwards trend with complete flattening beyond 65% PER. Could easily verify this with a binomial plot.