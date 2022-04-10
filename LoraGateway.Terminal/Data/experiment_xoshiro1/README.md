Observations:
- Most runs were cut off causing the last generation(s)' success to be marked as False. This can easily be fixed by bumping the timeout when an is update received.
- The new PRNG was only tested with static seed across generations - we should not reset the state between to create more richness of random values
- The plot looks beautiful
- Plotting generation success would be awesome

To be done:
- Replot existing CSV
- Plot generation success
- Check on CRC Fail OR use short timeout and retransmit command if this received with max retries of 3 (stopping experiment)
- Timeout much lower, 'kick it' with Update commands like petting a watchdog
