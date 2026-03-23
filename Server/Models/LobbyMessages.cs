using System.Text.Json.Serialization;

namespace OpenRocketArena.Server.Models;

public class LobbyMessage
{
    [JsonPropertyName("_msgtype")]
    public string MsgType { get; set; } = string.Empty;
    
    [JsonPropertyName("X-Amzn-Trace-Id")]
    public string TraceId { get; set; } = string.Empty;
}

public class LobbyInfoRequest : LobbyMessage
{
}

public class LobbyInfoResponse : LobbyMessage
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("leaderID")]
    public string LeaderId { get; set; } = string.Empty;

    [JsonPropertyName("partyMembers")]
    public List<string>? PartyMembers { get; set; }

    [JsonPropertyName("invitedPlayers")]
    public List<string>? InvitedPlayers { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("user_data")]
    public Dictionary<string, string>? UserData { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }

    [JsonPropertyName("sessionID")]
    public string SessionId { get; set; } = string.Empty;
}

public class LobbyCreateRequest : LobbyMessage
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}

public class LobbyCreateResponse : LobbyMessage
{
    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("leaderID")]
    public string LeaderId { get; set; } = string.Empty;

    [JsonPropertyName("partyMembers")]
    public List<string> PartyMembers { get; set; } = [];

    [JsonPropertyName("invitedPlayers")]
    public List<string> InvitedPlayers { get; set; } = [];

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("user_data")]
    public Dictionary<string, string> UserData { get; set; } = new();

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyPartyInvitationAck : LobbyMessage
{
    [JsonPropertyName("msgId")]
    public string MsgId { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("sent")]
    public bool Sent { get; set; }
}

public class LobbyPartyInvitationSent : LobbyMessage
{
    [JsonPropertyName("msg")]
    public LobbyPartyInvitationAck Msg { get; set; } = new();
}

public class LobbyPartyInvitationTimeout : LobbyMessage
{
    [JsonPropertyName("msg")]
    public LobbyPartyInvitationAck Msg { get; set; } = new();
}

public class LobbyInviteRequest : LobbyMessage
{
    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;
}

public class LobbyPartyInvitationAnnouncement : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;
}

public class LobbyInviteResponse : LobbyMessage
{
    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }

    [JsonPropertyName("xrayTraceId")]
    public string XrayTraceId { get; set; } = string.Empty;
}

public class LobbyAcceptPartyInvitationAnnouncement : LobbyMessage
{
    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("playerPresence")]
    public Dictionary<string, string> PlayerPresence { get; set; } = new();
}

public class LobbyJoinRequest : LobbyMessage
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;
}

public class LobbyJoinResponse : LobbyMessage
{
    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("leaderID")]
    public string LeaderId { get; set; } = string.Empty;

    [JsonPropertyName("partyMembers")]
    public List<string> PartyMembers { get; set; } = [];

    [JsonPropertyName("invitedPlayers")]
    public List<string> InvitedPlayers { get; set; } = [];

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("user_data")]
    public Dictionary<string, string> UserData { get; set; } = new();

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("inviterID")]
    public string InviterId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyJoinPartyAnnouncement : LobbyMessage
{
    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("playerPresence")]
    public Dictionary<string, string> PlayerPresence { get; set; } = new();
}

public class LobbyLeaveRequest : LobbyMessage
{
}

public class LobbyLeavePartyAnnouncement : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;
}

public class LobbyLeaveResponse : LobbyMessage
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyPromotePartyAnnouncement : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;
}

public class LobbyKickRequest : LobbyMessage
{
    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;
}

public class LobbyKickPartyAnnouncement : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;
}

public class LobbyKickResponse : LobbyMessage
{
    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbySetUserDataRequest : LobbyMessage
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

public class LobbySetUserDataResponse : LobbyMessage
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyUserDataAnnouncement : LobbyMessage
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;
}

public class LobbySetDataRequest : LobbyMessage
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}

public class LobbySetDataResponse : LobbyMessage
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyGetDataRequest : LobbyMessage
{
}

public class LobbyGetDataResponse : LobbyMessage
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyDataPartyAnnouncement : LobbyMessage
{
    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;

    [JsonPropertyName("partyID")]
    public string PartyId { get; set; } = string.Empty;

    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;
}

public class LobbyRemovePlayer : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;
}

public class LobbyPromoteRequest : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;
}

public class LobbyPromoteResponse : LobbyMessage
{
    [JsonPropertyName("playerID")]
    public string PlayerId { get; set; } = string.Empty;

    [JsonPropertyName("friendID")]
    public string FriendId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyServerShutDown : LobbyMessage
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class LobbyErrorResponse : LobbyMessage
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("response_code")]
    public int Code { get; set; }
}

public class LobbyConnectionStateChangedEvent : LobbyMessage
{
    [JsonPropertyName("previousState")]
    public int PreviousState { get; set; }

    [JsonPropertyName("currentState")]
    public int CurrentState { get; set; }
}

public class LobbyGetBulkPlayerPartyInfoResponse : LobbyMessage
{
    [JsonPropertyName("Players")]
    public Dictionary<string, LobbyInfoResponse> Players { get; set; } = new();
}

public class LobbyGetPlayerPresenceBulkResponse : LobbyMessage
{
    [JsonPropertyName("player_presences")]
    public Dictionary<string, Dictionary<string, string>> PlayerPresences { get; set; } = new();
}
