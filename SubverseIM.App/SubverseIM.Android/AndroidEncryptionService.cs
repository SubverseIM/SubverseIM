using Android.Security.Keystore;
using AndroidX.Biometric;
using Java.Lang;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;
using SubverseIM.Services;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android
{
    public class AndroidEncryptionService : IEncryptionService
    {
        private const string KEY_NAME = "SubverseIM-Database";

        private const string DEFAULT_PROVIDER = "AndroidKeyStore";

        private const int VALID_KEY_DURATION_SECONDS = 30;

        private class AuthenticationCallback : BiometricPrompt.AuthenticationCallback
        {
            private readonly TaskCompletionSource<string?> resultTcs;

            private readonly string passwordFilePath;

            public AuthenticationCallback(string passwordFilePath)
            {
                this.passwordFilePath = passwordFilePath;
                resultTcs = new();
            }

            public async Task<string?> GetResultAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await resultTcs.Task.WaitAsync(cancellationToken);
            }

            public override void OnAuthenticationError(int errorCode, ICharSequence errString)
            {
                base.OnAuthenticationError(errorCode, errString);
                resultTcs.SetException(new AuthenticationResultException(errorCode, errString.ToString()));
            }

            public override void OnAuthenticationFailed()
            {
                base.OnAuthenticationFailed();
                resultTcs.SetResult(null);
            }

            public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
            {
                base.OnAuthenticationSucceeded(result);
                if (File.Exists(passwordFilePath))
                {
                    byte[] encryptedKey = File.ReadAllBytes(passwordFilePath);
                    byte[]? decryptedKey = result.CryptoObject?.Cipher?.DoFinal(encryptedKey);

                    string? password = decryptedKey is null ? null : Convert.ToBase64String(decryptedKey);
                    resultTcs.SetResult(password);
                }
                else 
                {
                    byte[] decryptedKey = RandomNumberGenerator.GetBytes(32);
                    byte[]? encryptedKey = result.CryptoObject?.Cipher?.DoFinal(decryptedKey);
                    byte[]? initializationVector = result.CryptoObject?.Cipher?.GetIV();

                    if (encryptedKey is not null && initializationVector is not null) 
                    {
                        File.WriteAllBytes(passwordFilePath, [..encryptedKey, ..initializationVector]);
                    }

                    string password = Convert.ToBase64String(decryptedKey);
                    resultTcs.SetResult(password);
                }
            }
        }

        private readonly AuthenticationCallback authenticationCallback;

        private readonly BiometricPrompt biometricPrompt;

        private readonly string passwordFilePath;

        public AndroidEncryptionService(MainActivity mainActivity)
        {
            string appDataDirPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            passwordFilePath = Path.Combine(appDataDirPath, "SubverseIM.key");

            authenticationCallback = new(passwordFilePath);

            biometricPrompt = new BiometricPrompt(mainActivity, authenticationCallback);
        }

        private IKey? GenerateSecretKey()
        {
            var spec = new KeyGenParameterSpec.Builder(KEY_NAME,
                 KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .SetKeySize(256)
                .SetUserAuthenticationRequired(true)
                .Build();

            KeyGenerator? keyGenerator = KeyGenerator.GetInstance(
                    KeyProperties.KeyAlgorithmAes, DEFAULT_PROVIDER);
            keyGenerator?.Init(spec);
            return keyGenerator?.GenerateKey();
        }

        private IKey? GetSecretKey()
        {
            using KeyStore? keyStore = KeyStore.GetInstance(DEFAULT_PROVIDER);
            keyStore?.Load(null);

            return keyStore?.GetKey(KEY_NAME, null);
        }

        private Cipher? GetCipher()
        {
            return Cipher.GetInstance(KeyProperties.KeyAlgorithmAes + "/"
                    + KeyProperties.BlockModeGcm + "/"
                    + KeyProperties.EncryptionPaddingNone);
        }

        public async Task<string?> GetEncryptionKeyAsync(CancellationToken cancellationToken)
        {
            var promptInfo = new BiometricPrompt.PromptInfo.Builder()
                        .SetTitle("Authenticate SubverseIM")
                        .SetSubtitle("Please authenticate to decrypt your data.")
                        .SetDescription("SubverseIM uses the device's lock screen credentials to keep your data private and safe.")
                        .SetAllowedAuthenticators(
                            BiometricManager.Authenticators.BiometricStrong | 
                            BiometricManager.Authenticators.DeviceCredential
                            )
                        .Build();

            Cipher cipher = GetCipher()!;
            IKey secretKey = GetSecretKey() ?? GenerateSecretKey()!;
            if (File.Exists(passwordFilePath))
            {
                byte[] initializationVector = File.ReadAllBytes(passwordFilePath)[^16..];
                cipher.Init(Javax.Crypto.CipherMode.DecryptMode, secretKey, new GCMParameterSpec(128, initializationVector));
            }
            else
            {
                cipher.Init(Javax.Crypto.CipherMode.EncryptMode, secretKey);
            }

            biometricPrompt.Authenticate(promptInfo, new BiometricPrompt.CryptoObject(cipher));
            return await authenticationCallback.GetResultAsync(cancellationToken);
        }
    }
}
