Plan is to create bigger generations
Need to increase timeout probably

- [x] Detect generation timeout (CRC failue) is currently a bad factor with 1 out of 50 chance
- [x] Detect that CRC issues happen with high chance and mostly on PC transmissions
- [x] Spot lost frames (timing, radio RX/TX glitches or LoRa...) 

Conclusions on decoding:
- This LFSR seed 0x08 is highly sensitive for mathematical loss beyond 8 frames
-> If a certain packet is lost, decoding failure has super high probability
-> The packets beyond count 10 of 18 are completely useless basically (9 and 10 are very finicky)
We should be able to determine this offline already by testing our new encoding vectors and randoming the LFSR state 'from a bag' (so to avoid already used states)
- Have to try new LFSR seeds as well to verify the gen-8 breaking point

Conclusions on measurement setup:
- The flash storage can not exceed 16380 samples, so might check this beforehand

Conclusions on PER coin flipper:
- Probability of failure is not linear but binomial for bernouilli coin flips, so resulting chance of succesful decoding of a generation definitely is NOT linear with PER.
https://homepage.divms.uiowa.edu/~mbognar/applets/bin.html
- The PER spread looks quite good for higher generation sizes