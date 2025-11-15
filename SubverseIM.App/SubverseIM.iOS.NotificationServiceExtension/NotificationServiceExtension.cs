using PgpCore;
using SubverseIM.Core;
using UserNotifications;

namespace SubverseIM.iOS
{
    [Register("NotificationServiceExtension")]
    public class NotificationServiceExtension : UNNotificationServiceExtension
    {
        public const string EXTRA_CONTENT_ID = "BODY_CONTENT";

        public const string EXTRA_SENDER_ID = "SENDER_ID";

        public NotificationServiceExtension() : base(NSObjectFlag.Empty)
        {
        }

        public override void DidReceiveNotificationRequest(UNNotificationRequest request, Action<UNNotificationContent> contentHandler)
        {
            UNMutableNotificationContent mutableContent = (UNMutableNotificationContent)request.Content.MutableCopy();
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
                        myKeyContainer = new(privateKeyFileStream, "#FreeTheInternet");
                    }
                }
                else
                {
                    myKeyContainer = null;
                }

                SubversePeerId peerId = SubversePeerId.FromString((NSString)request.Content.UserInfo[EXTRA_SENDER_ID]);
                contacts.TryGetValue(peerId, out string? displayName);

                mutableContent.Subtitle = displayName ?? "Anonymous";
                using (PGP pgp = new(myKeyContainer))
                {
                    string messageContent = pgp.Decrypt((NSString)request.Content.UserInfo[EXTRA_CONTENT_ID]);
                    if (Uri.IsWellFormedUriString(messageContent, UriKind.Absolute))
                    {
                        mutableContent.Body = "[embed]";
                    }
                    else
                    {
                        mutableContent.Body = messageContent;
                    }
                }
            }
            catch (Exception ex)
            {
                mutableContent.Title = "Error";
                mutableContent.Subtitle = ex.GetType().FullName ?? "Unknown Exception Type";
                mutableContent.Body = ex.Message;
            }
            finally
            {
                contentHandler(mutableContent);
            }
        }

        public override void TimeWillExpire()
        {
            // Called just before the extension will be terminated by the system.
            // Use this as an opportunity to deliver your "best attempt" at modified content, otherwise the original push payload will be used.
        }
    }
}
