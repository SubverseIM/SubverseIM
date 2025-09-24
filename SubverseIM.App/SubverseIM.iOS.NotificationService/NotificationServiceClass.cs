using ObjCRuntime;
using PgpCore;
using SubverseIM.Core;
using SubverseIM.Services;
using UserNotifications;

namespace NotificationServiceExtension
{
    [Register("NotificationService")]
    public class NotificationService : UNNotificationServiceExtension
    {
        protected NotificationService(NativeHandle handle) : base(handle)
        {
            // Note: this .ctor should not contain any initialization logic,
            // it only exists so that the OS can instantiate an instance of this class.
        }

        public override void DidReceiveNotificationRequest(UNNotificationRequest request, Action<UNNotificationContent> contentHandler)
        {
            var mutableRequest = (UNMutableNotificationContent)request.Content.MutableCopy();
            try
            {
                NSUrl? appGroupContainer = NSFileManager.DefaultManager.GetContainerUrl("group.com.chosenfewsoftware.SubverseIM");
                string? baseFilePath = appGroupContainer?.Path;

                EncryptionKeys? myKeyContainer;
                Dictionary<SubversePeerId, string> contacts = new();
                if (baseFilePath is not null)
                {
                    string csvFilePath = Path.Combine(baseFilePath, "contacts.csv");
                    using (StreamReader csvReader = File.OpenText(csvFilePath))
                    {
                        string? entryFullStr = csvReader.ReadLine();
                        while (!string.IsNullOrEmpty(entryFullStr))
                        {
                            string entryPeerIdStr = entryFullStr.Substring(0, 40);
                            SubversePeerId entryPeerId = SubversePeerId.FromString(entryPeerIdStr);

                            string entryNameStr = entryFullStr.Substring(41);
                            contacts.Add(entryPeerId, entryNameStr);

                            entryFullStr = csvReader.ReadLine();
                        }
                    }

                    string privateKeyFilePath = Path.Combine(baseFilePath, "private-key.data");
                    using (FileStream privateKeyFileStream = File.OpenRead(privateKeyFilePath))
                    {
                        myKeyContainer = new(privateKeyFileStream, IDbService.SECRET_PASSWORD);
                    }
                }
                else
                {
                    myKeyContainer = null;
                }

                SubversePeerId peerId = SubversePeerId.FromString(request.Content.Subtitle);
                contacts.TryGetValue(peerId, out string? displayName);

                mutableRequest.Subtitle = displayName ?? "Anonymous";
                using (PGP pgp = new(myKeyContainer))
                {
                    mutableRequest.Body = pgp.Decrypt(request.Content.Body);
                }
            }
            catch
            {
                mutableRequest.Subtitle = "Somebody";
                mutableRequest.Body = "[Contents Encrypted]";
                throw;
            }
            finally
            {
                contentHandler(mutableRequest);
            }
        }

        public override void TimeWillExpire()
        {
            // Called just before the extension will be terminated by the system.
            // Use this as an opportunity to deliver your "best attempt" at modified content, otherwise the original push payload will be used.
        }
    }
}
