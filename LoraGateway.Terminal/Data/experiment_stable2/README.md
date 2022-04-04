Plan is to create bigger generations
Need to increase timeout probably

- [x] Detect generation timeout (CRC failue) is currently a bad factor with 1 out of 50 chance
- [x] Detect that CRC issues happen with high chance and mostly on PC transmissions
- [x] Spot lost frames (timing, radio RX/TX glitches or LoRa...) 

Conclusions:
- This LFSR seed 0x08 is highly sensitive for mathematical loss beyond 8 frames
-> If a certain packet is lost, the rest 'are completely useless' basically
-> 