// See https://aka.ms/new-console-template for more information
using SubverseIM.Calculator;
using System.Numerics;
using System.Security.Cryptography;

const int MIN_N = 2, MAX_N = 8;
const int MIN_K = 1, MAX_K = 16;

using StreamWriter writer = File.CreateText("results.csv");
writer.AutoFlush = true;

void WriteLine(string? s)
{
    Console.WriteLine(s);
    lock (writer)
    {
        writer.WriteLine(s);
    }
}

WriteLine("  N,  K, P(N:K)");
Parallel.For(MIN_N, MAX_N + 1, N =>
{
    ulong ident = BitMatrix.Identity(N).Bits;
    IEnumerable<ulong> PermuteBits(Random rng, int M, int K)
    {
        if (K == 0)
        {
            yield return 0UL;
            yield break;
        }

        for (int i = K - 1; i < M; i++)
        {
            foreach (ulong v in PermuteBits(rng, i, K - 1).OrderBy(v => rng.Next()))
            {
                ulong r = (1UL << i) | v;
                if ((r & ident) == 0)
                {
                    yield return r;
                }
            }
        }
    }

    Parallel.For(MIN_K, MAX_K + 1, K =>
    {
        Random rng = new();
        long totalPopCount = 0, totalPermCount = 0;

        IEnumerable<ulong> V = PermuteBits(rng, N * N, K);
        Parallel.ForEach(V.Take(1 << 24), v =>
        {
            BitMatrix A = new(N, v);
            BitMatrix R_n = BitMatrix.Warshall(A);
            
            Interlocked.Add(ref totalPopCount, BitOperations.PopCount(R_n.Bits & ~ident));
            Interlocked.Increment(ref totalPermCount);
        });

        double P = totalPopCount / (double)(totalPermCount * N * (N - 1));
        WriteLine($"{N,3},{K,3},{P,7:F3}");
    });
});
