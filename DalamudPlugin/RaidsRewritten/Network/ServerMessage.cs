using Dalamud.Game.Text;

namespace RaidsRewritten.Network;

public struct ServerMessage
{
    public struct Payload
    {
        public enum Action : int
        {
            None = 0,
            UpdatePlayersInRoom = 1,

            Close = 10,
        }

        public struct ChatMessagePayload
        {
            public XivChatType chatType;
            public byte[] message;
        }

        public Action action;
        public string[] players;
    }

    public string from;
    public string target;
    public Payload payload;
}
