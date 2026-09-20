using TMPro;
using UnityEngine;

/// <summary>Tap/click conversation for every civilian pedestrian.</summary>
public sealed class SocialNpc : MonoBehaviour
{
    static readonly string[] StreetNames = { "LOCAL RESIDENT", "COMMUTER", "SHOP WORKER", "PASSER-BY" };
    static readonly string[] StreetLines =
    {
        "Busy streets today. Keep an eye on the district markers.",
        "The rival crews usually gather around contested territory.",
        "The pub and stadium are where most of the local activity happens.",
        "Police patrol the main roads when the district gets heated."
    };
    static readonly string[] StadiumNames = { "HOME SUPPORTER", "MATCHDAY STEWARD", "PROGRAMME SELLER", "FOOD VENDOR" };
    static readonly string[] StadiumLines =
    {
        "The turnstiles are filling up. Matchday is alive around the whole ground.",
        "Supporters are gathering by the queue lanes and vendor stalls.",
        "You can follow the stadium marker from anywhere in the district.",
        "Big crowd today. Keep the approach clear for your crew."
    };
    static readonly string[] PubNames = { "PUB REGULAR", "LOCAL SUPPORTER", "BAR STAFF", "NEIGHBOUR" };
    static readonly string[] PubLines =
    {
        "The outdoor tables fill up before every match.",
        "Rallying at the pub can lift the crew's morale.",
        "People here hear plenty about rival movement around town.",
        "The district feels safer when your territory is clearly controlled."
    };
    static readonly string[] ArrivalNames = { "DOCK WORKER", "TRAVELLER", "LOCAL GUIDE", "STREET VENDOR" };
    static readonly string[] ArrivalLines =
    {
        "This is the arrival point. The marked streets lead into the district.",
        "Rival groups are easier to spot by their red ground rings.",
        "Use the map markers before moving deeper into unfamiliar territory.",
        "Police and traffic use the main roads, so watch your route."
    };
    static readonly string[] MarketNames = { "MARKET TRADER", "LOCAL SHOPPER", "STALL HOLDER", "DELIVERY RUNNER" };
    static readonly string[] MarketLines =
    {
        "The market is busy before kickoff. Supplies move quickly here.",
        "Local events bring supporters and families through this square.",
        "Runners can gather supplies faster while organizers work the crowd.",
        "People trade district news here as often as they trade goods."
    };
    static readonly string[] ParkNames = { "PARK REGULAR", "COMMUNITY VOLUNTEER", "DOG WALKER", "STREET COACH" };
    static readonly string[] ParkLines =
    {
        "The community meetup is starting. Helping out improves your local reputation.",
        "This is a good place to organize people without raising police heat.",
        "Scouts often watch the connecting streets from the park.",
        "Not every useful job is a fight. The district remembers who helps."
    };
    static readonly string[] TransitNames = { "COMMUTER", "COACH DRIVER", "TICKET CLERK", "TRAVEL STEWARD" };
    static readonly string[] TransitLines =
    {
        "Check the route before departure. A prepared transport plan makes the return safer.",
        "The queues change when police close a street, so gather intel first.",
        "Match tickets and transport need organizing before the stadium rush.",
        "Runners are quickest at sorting vehicles, bags and meeting points."
    };
    static readonly string[] FoodNames = { "FOOD VENDOR", "STREET MUSICIAN", "MATCHDAY VISITOR", "LOCAL FAMILY" };
    static readonly string[] FoodLines =
    {
        "The food stalls and music keep the area active even when no fight is happening.",
        "A good atmosphere brings people together and improves supporter momentum.",
        "There are community events, ticket queues and travel plans to handle today.",
        "The stadium approach feels alive when every small activity is running."
    };

    string venue = "CITY STREET";
    string displayName;
    string dialogue;
    TextMeshPro prompt;
    bool invitationAttempted;
    bool joinedCrew;

    public string Venue => venue;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "LOCAL RESIDENT" : displayName;
    public bool IsTalkable => true;
    public bool HasVisiblePrompt => true;
    public bool InvitationAttempted => invitationAttempted;
    public bool JoinedCrew => joinedCrew;

    public void Configure(string location, bool showPrompt)
    {
        venue = string.IsNullOrWhiteSpace(location) ? "CITY STREET" : location.ToUpperInvariant();
        int index = GetInstanceID() & int.MaxValue;
        string[] names;
        string[] lines;
        if (venue.Contains("MARKET")) { names = MarketNames; lines = MarketLines; }
        else if (venue.Contains("PARK")) { names = ParkNames; lines = ParkLines; }
        else if (venue.Contains("TRANSIT") || venue.Contains("RAIL")) { names = TransitNames; lines = TransitLines; }
        else if (venue.Contains("FOOD") || venue.Contains("WATERFRONT")) { names = FoodNames; lines = FoodLines; }
        else if (venue.Contains("STADIUM")) { names = StadiumNames; lines = StadiumLines; }
        else if (venue.Contains("PUB")) { names = PubNames; lines = PubLines; }
        else if (venue.Contains("ARRIVAL") || venue.Contains("DOCK")) { names = ArrivalNames; lines = ArrivalLines; }
        else { names = StreetNames; lines = StreetLines; }
        displayName = names[index % names.Length];
        dialogue = lines[(index / names.Length) % lines.Length];
        if (prompt) prompt.gameObject.SetActive(false);
    }

    public void Talk()
    {
        if (string.IsNullOrWhiteSpace(displayName)) Configure(venue, false);
        var conversation=NpcConversationUI.Instance;
        if(conversation && conversation.TryOpen(this))return;
        CityGameplay.Instance?.PostEvent("MOVE A CREW MEMBER CLOSER TO TALK");
    }

    public string Greeting => dialogue;

    public string ReplyTo(string question)
    {
        string q=(question??string.Empty).ToLowerInvariant();
        int variant=(GetInstanceID()&int.MaxValue)%3;
        if(q.Contains("weather")||q.Contains("rain")||q.Contains("sun"))
            return variant==0?"Looks dry for now, but the wind is picking up near the ground.":variant==1?"Clouds are coming over. Good weather for keeping the streets busy.":"It should stay clear through the next matchday rush.";
        if(q.Contains("stadium")||q.Contains("match")||q.Contains("game")||q.Contains("football"))
            return venue.Contains("STADIUM")?"The queues are building, the vendors are open and the supporters are gathering around the approaches.":"Follow the stadium marker. The crowd usually starts gathering well before kickoff.";
        if(q.Contains("pub")||q.Contains("drink")||q.Contains("bar"))
            return venue.Contains("PUB")?"This place gets busy before the match. Locals trade news and crews rally outside.":"The local pub is a social hub. You will hear plenty about the district there.";
        if(q.Contains("event")||q.Contains("upcoming")||q.Contains("today"))
            return venue.Contains("MARKET")?"The market and community tables are open before the supporter meetup.":
                venue.Contains("PARK")?"A community meetup is running here before the matchday crowd arrives.":
                venue.Contains("TRANSIT")?"Ticket collection and coach departures are the main events at this stop.":
                "There is a home match coming up. Expect markets, supporter meetups, traffic and police activity around the main routes.";
        if(q.Contains("area")||q.Contains("street")||q.Contains("rival")||q.Contains("police"))
            return AreaHint();
        if(q.Contains("hello")||q.Contains("hi")||q.Contains("how are"))
            return "All good. The streets are busy today. Ask me about the area, the match, the pub or upcoming events.";
        return "I have heard people talking about the matchday crowd, the local pub, the weather and activity around this district.";
    }

    public bool TryInvite(float roll, out string response)
    {
        if(joinedCrew){response="I already said yes. Let us get moving.";return true;}
        if(invitationAttempted){response="I have made up my mind. Not today.";return false;}
        if(BattleManager.instance==null || BattleManager.instance.PlayerAgents.Count>=BattleManager.instance.maxPlayerAgents)
        {response="Your active crew is full. Make room before asking me to join.";return false;}
        invitationAttempted=true;
        if(!AcceptsInvitation(roll))
        {response="No thanks. I am staying out of crew business today.";return false;}
        var recruit=BattleManager.instance.SpawnRecruitedAgentAt(transform.position,DisplayName);
        if(!recruit){response="I would join, but your active crew is full.";return false;}
        joinedCrew=true;
        response="All right, I am in. I will join your crew.";
        CityGameplay.Instance?.PostEvent(DisplayName+" JOINED YOUR CREW");
        return true;
    }

    public static bool AcceptsInvitation(float roll)=>roll>=0f&&roll<.25f;

    public bool IsEngaged { get; private set; }

    public void PauseForConversation(bool paused,Vector3 speakerPosition)
    {
        IsEngaged = paused;
        var pedestrian=GetComponent<PedestrianController>();
        if(pedestrian)pedestrian.SetConversationPaused(paused,speakerPosition);
    }

    public void FinishJoinedConversation()
    {
        if(joinedCrew)Destroy(gameObject);
    }

    string AreaHint()
    {
        if (venue.Contains("STADIUM")) return "Supporters, vendors and stewards are active around the stadium approach.";
        if (venue.Contains("PUB")) return "The pub is a social hub. Crews rally here and locals share district news.";
        if (venue.Contains("ARRIVAL") || venue.Contains("DOCK")) return "Start from the marked arrival area, identify red-ring rivals, then move street by street.";
        if (venue.Contains("MARKET")) return "The market supports supply runs and community activity. Talk to traders for local information.";
        if (venue.Contains("PARK")) return "The park hosts community events and gives scouts a view of connecting streets.";
        if (venue.Contains("TRANSIT") || venue.Contains("RAIL")) return "Transport preparation links tickets, routes and the return journey.";
        if (venue.Contains("FOOD") || venue.Contains("WATERFRONT")) return "Food stalls, music and visitors build social momentum around this district.";
        return "Green rings mark your crew, red rings mark rivals, and blue markers identify police presence.";
    }

    void LateUpdate()
    {
        if (prompt && Camera.main) prompt.transform.rotation = Camera.main.transform.rotation;
    }
}
