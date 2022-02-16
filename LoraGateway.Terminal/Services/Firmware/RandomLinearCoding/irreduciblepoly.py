# Python3 program for the above approach
 
# Function to to implement the sieve
# of eratosthenes
def SieveOfEratosthenes(M):
   
    # Stores the prime numbers
    isPrime = [True]*(M + 1)
 
    for p in range(2, M + 1):
        if p * p > M:
            break
 
        # If isPrime[p] is not changed,
        # then it is a prime
        if (isPrime[p] == True):
 
            # Update all multiples of
            # p as non-prime
            for i in range(p * p, M + 1, p):
                isPrime[i] = False
 
    # Stores all prime numbers less
    # than M
    prime = []
 
    for i in range(2, M + 1):
       
        # If the i is the prime numbers
        if (isPrime[i]):
            prime.append(i)
 
    # Return array having the primes
    return prime
 
# Function to check whether the three
# conditions of Eisenstein's
# Irreducibility criterion for prime P
def check(A, P, N):
    # 1st condition
    if (A[0] % P == 0):
        return 0
 
    # 2nd condition
    for i in range(1,N):
        if (A[i] % P):
            return 0
 
    # 3rd condition
    if (A[N - 1] % (P * P) == 0):
        return 0
 
    return 1
# Function to check for Eisensteins
# Irreducubility Criterion
def checkIrreducibilty(A, N):
    # Stores the largest element in A
    M = -1
 
    # Find the maximum element in A
    for i in range(N):
        M = max(M, A[i])
 
    # Stores all the prime numbers
    primes = SieveOfEratosthenes(M + 1)
 
    # Check if any prime
    # satisfies the conditions
    for i in range(len(primes)):
 
        # Function Call to check
        # for the three conditions
        if (check(A, primes[i], N)):
            return 1
    return 0
 
# Driver Code
if __name__ == '__main__':
    A = [4, 7, 21, 28]
    N = len(A)
    print (checkIrreducibilty(A, N))
    B = [1, 0, 0, 0, 1, 1, 1, 0, 1]
    N2=len(B)
    print(checkIrreducibilty(B,N2))