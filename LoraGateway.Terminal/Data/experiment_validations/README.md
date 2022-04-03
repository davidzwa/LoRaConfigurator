03 Apr 2022
Experiment 3 I found that the PER for missing generations was set to 1.0f and corrected that with DecodingUpdates to have better (lower) estimate)

Experiment 4 
Adjusted the PER range to 0.25 - 0.85
I noticed without intermediate results some data points were missing altogether due to no single result being triggered.
Experiment halted after 0.65 (real ~0.58%) PER

Experiment 5
Now filled gaps at end of experiment - looking quite good now
- [x] Next I will make upper bound included for runs
- [x] Will plot expected PER as well next
- [ ] After I will plot boxplot instead of avg