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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Android
{
    public class AndroidEncryptionService : IEncryptionService
    {
        private const string KEY_NAME = "com.chosenfewsoftware.SubverseIM";

        private const string ANDROID_KEY_STORE = "AndroidKeyStore";

        private const int VALIDITY_DURATION_SECONDS = 30;

        private const int KEY_SIZE = 256;

        private class AuthenticationKeyData 
        {
            public string? CipherText { get; set; }

            public string? IV { get; set; }
        }

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

                Cipher? cipher = GetCipher();
                IKey? secretKey = GetSecretKey() ?? GenerateSecretKey();

                if (File.Exists(passwordFilePath))
                {
                    string jsonStr = File.ReadAllText(passwordFilePath);
                    AuthenticationKeyData? authenticationKeyData = 
                        JsonSerializer.Deserialize<AuthenticationKeyData>(jsonStr);

                    byte[]? encryptedKey = authenticationKeyData?.CipherText is null ?
                        null : Convert.FromBase64String(authenticationKeyData.CipherText);
                    byte[]? iv = authenticationKeyData?.IV is null ? 
                        null : Convert.FromBase64String(authenticationKeyData.IV);

                    cipher?.Init(Javax.Crypto.CipherMode.DecryptMode, secretKey,
                            new IvParameterSpec(iv)
                            );
                    byte[]? decryptedKey = cipher?.DoFinal(encryptedKey);

                    string? password = decryptedKey is null ? null : Convert.ToBase64String(decryptedKey);
                    resultTcs.SetResult(password);
                }
                else
                {
                    cipher?.Init(Javax.Crypto.CipherMode.EncryptMode, secretKey);
                    byte[] decryptedKey = RandomNumberGenerator.GetBytes(32);

                    byte[]? encryptedKey = cipher?.DoFinal(decryptedKey);
                    byte[]? initializationVector = cipher?.GetIV();

                    if (encryptedKey is not null && initializationVector is not null)
                    {
                        AuthenticationKeyData authenticationKeyData = new()
                        {
                            CipherText = Convert.ToBase64String(encryptedKey),
                            IV = Convert.ToBase64String(initializationVector)
                        };

                        string jsonStr = JsonSerializer.Serialize(authenticationKeyData);
                        File.WriteAllText(passwordFilePath, jsonStr);
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
            string? appDataDirPath = mainActivity.GetExternalFilesDir(null)?.AbsolutePath ??
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            passwordFilePath = Path.Combine(appDataDirPath, "SubverseIM-key.json");

            authenticationCallback = new(passwordFilePath);

            biometricPrompt = new BiometricPrompt(mainActivity, authenticationCallback);
        }

        private static IKey? GenerateSecretKey()
        {
            KeyGenParameterSpec.Builder builder =
                new KeyGenParameterSpec.Builder(KEY_NAME,
                 KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeCbc)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingPkcs7)
                .SetKeySize(KEY_SIZE)
                .SetUserAuthenticationRequired(true);

            KeyGenParameterSpec spec;
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                spec = builder
                    .SetUserAuthenticationParameters(VALIDITY_DURATION_SECONDS, (int)
                        (KeyPropertiesAuthType.BiometricStrong |
                        KeyPropertiesAuthType.DeviceCredential))
                    .Build();
            }
            else
            {
                spec = builder
                    .SetUserAuthenticationValidityDurationSeconds(VALIDITY_DURATION_SECONDS)
                    .Build();
            }

            KeyGenerator? keyGenerator = KeyGenerator.GetInstance(
                    KeyProperties.KeyAlgorithmAes, ANDROID_KEY_STORE);
            keyGenerator?.Init(spec);
            return keyGenerator?.GenerateKey();
        }

        private static IKey? GetSecretKey()
        {
            KeyStore? keyStore = KeyStore.GetInstance(ANDROID_KEY_STORE);
            keyStore?.Load(null);

            return keyStore?.GetKey(KEY_NAME, null);
        }

        private static Cipher? GetCipher()
        {
            return Cipher.GetInstance(KeyProperties.KeyAlgorithmAes + "/"
                    + KeyProperties.BlockModeCbc + "/"
                    + KeyProperties.EncryptionPaddingPkcs7);
        }

        public async Task<string?> GetEncryptionKeyAsync(CancellationToken cancellationToken)
        {
            var promptInfo = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle("Authenticate SubverseIM")
                .SetSubtitle("Please authenticate to decrypt your data.")
                .SetDescription("SubverseIM uses the device's lock screen credentials to keep your data private and safe.")
                .SetAllowedAuthenticators(
                    BiometricManager.Authenticators.BiometricStrong |
                    BiometricManager.Authenticators.BiometricWeak |
                    BiometricManager.Authenticators.DeviceCredential)
                .Build();
            biometricPrompt.Authenticate(promptInfo);

            return await authenticationCallback.GetResultAsync(cancellationToken);
        }
    }
}
