Went for longer experiments but now 10% PER step. Also went for redundancy 300% max.

The aim:
- 2x more generations per PER
- 1/2x fine-grained step (10% steps, 0%-80%)
- 2x extra redundancy (+100% => +300%)

Runs:
1 - Lets see if [0,20] shows anything weird.
> Ran fine but had a bug that the statically allocated gen update vector was sized to gen size and not to gen count
> Also I adjusted any missing packets when failure so the totals make sense for 100% failure
2 - Accidentaly ran twice, but fixed color scheme to be more discrete steps
3 - Lets run [0,80]

4 - (4th of May 2022) Before I didn't save the data of previous PER iterations. I tried to fix that here, but introduced a bug.
I basically set the updates not to be cleared, but this messed up the PER calculation and all the rest.
I can fix that in post luckily and regenerate the plots.