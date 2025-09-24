using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace MatchfishingApp
{
    public class VolumeUpMessage : ValueChangedMessage<bool>
    {
        public VolumeUpMessage(bool value) : base(value) { }
    }
}
