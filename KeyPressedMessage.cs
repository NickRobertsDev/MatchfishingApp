using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MatchfishingApp
{
    public class KeyPressedMessage : ValueChangedMessage<string>
    {
        // payload is something like "KeyDown: F5" or "KeyUp: Ctrl"
        public KeyPressedMessage(string keyEvent) : base(keyEvent) { }
    }
}
