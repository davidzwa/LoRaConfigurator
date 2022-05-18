from math import comb
import csv
import numpy as np
import matplotlib.pyplot as plt

def P(m, n, r, q):
    p = 1.0
    for i in range(0, n+r):
        p *= (1 - 1 / pow(q, m-i))
    return p


def success_rate(n, N, eps, r, q):
    p_success = 0
    p_success_perfect = 0
    for m in range(n, N+1):
        outer_decoder_chance = P(m, n, r, q)
        # print(m, N, outer_decoder_chance)
        p_success += comb(N, m) * pow(eps, N - m) * \
            pow(1-eps, m) * outer_decoder_chance
        p_success_perfect += comb(N, m) * pow(eps, N - m) * pow(1-eps, m)

    return p_success, p_success_perfect

max_red = 60
vals = []
with open('gen_0_2PER.csv', newline='\n') as csvfile:
	reader = csv.DictReader(csvfile)

	for row in reader:
		vals.append(int(row['RedundancyUsed']))

PER = 0.2
delta_max = 60
threshold = 20
redundancies = range(0, delta_max+1)
failure_rates_0_256 = []
failure_rates_0_256_perfect = []
q = pow(2, 8)
for delta in redundancies:
    total_sent = threshold + delta
    imperfect, perfect = success_rate(threshold, total_sent, PER, 0, pow(2, 8))
    failure_rates_0_256.append(imperfect)
    failure_rates_0_256_perfect.append(perfect)
    print(imperfect - perfect)

hist, bins = np.histogram(vals, bins=max_red, range=[0, 60])
cum_hist = np.cumsum(hist)
plt.bar(bins[:-1], hist/max(hist), alpha=0.2, color='green', label='RLNC histogram')
plt.plot(bins[:-1], cum_hist/max(cum_hist), alpha=0.9, color='orange', label='RLNC measured')
plt.plot(redundancies, failure_rates_0_256, alpha=0.6, label='RLNC model')
# plt.plot(redundancies, failure_rates_0_256_perfect, '--', alpha=0.6)
# print(hist, bin_edges)
plt.grid(True)
plt.legend()
plt.xlabel('Redundant Packets sent')
plt.ylabel('Decoding Probability')
plt.title('Decoding success vs redundancy (PER 20%, n=20)')
plt.show()