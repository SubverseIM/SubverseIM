using System.Numerics;

namespace SubverseIM.Calculator
{
    internal struct BitMatrix
    {
        private readonly int _N;

        private ulong _bits;

        public BitMatrix(int N, ulong bits) 
        {
            _N = N;
            _bits = bits;
        }

        private ulong _mask(int row, int col) 
        {
            if (row >= _N || row < 0)
                throw new ArgumentOutOfRangeException(nameof(row));

            if (col >= _N || col < 0)
                throw new ArgumentOutOfRangeException(nameof(col));

            return 1UL << (row * _N + col);
        }

        public int N => _N;

        public ulong Bits => _bits;

        public int this[int row, int col] 
        {
            get => BitOperations.PopCount(_bits & _mask(row, col));
            set => _bits = (_bits & ~_mask(row, col)) | (ulong)
                Math.Clamp(value, 0, 1) * _mask(row, col);
        }

        public static BitMatrix Identity(int N)
        {
            ulong v = 1UL;
            for (int i = 1; i < N; i++)
            {
                v = (v << (N + 1)) | 1UL;
            }
            return new(N, v);
        }

        public static BitMatrix Warshall(BitMatrix A)
        {
            BitMatrix R_n = A;
            for (int k = 0; k < R_n.N; k++) 
            {
                for (int i = 0; i < R_n.N; i++)
                {
                    for (int j = 0; j < R_n.N; j++)
                    {
                        R_n[i, j] |= R_n[i, k] & R_n[k, j];
                    }
                }
            }
            return R_n;
        }
    }
}
