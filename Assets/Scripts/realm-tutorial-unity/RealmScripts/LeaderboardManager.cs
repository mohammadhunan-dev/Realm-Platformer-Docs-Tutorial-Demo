using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Realms;
using System.Linq;
using Realms.Sync;
using System.Threading.Tasks;
public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;
    private Realm realm;
    private VisualElement root;
    private ListView listView;
    private Label displayTitle;
    private string username;
    private bool isLeaderboardUICreated = false;
    private List<Stat> topStats;
    private IDisposable listenerToken;  // (Part 2 Sync): listenerToken is the token for registering a change listener on all Stat objects

    #region PublicMethods
    public static async Task<Realm> GetRealm()
    {
        var syncConfiguration = new SyncConfiguration("UnityTutorialPartition", RealmController.syncUser);
        return await Realm.GetInstanceAsync(syncConfiguration);
    }

    // SetLoggedInUser() is a method that opens a realm, calls the CreateLeaderboardUI() method to create the LeaderboardUI and adds it to the Root Component
    // SetLoggedInUser()  takes a userInput, representing a username, as a parameter
    public async void SetLoggedInUser(string userInput)
    {
        username = userInput;
        realm = await GetRealm();

        // only create the leaderboard on the first run, consecutive restarts/reruns will already have a leaderboard created
        if (isLeaderboardUICreated == false)
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            CreateLeaderboardUI();
            root.Add(displayTitle);
            root.Add(listView);
            isLeaderboardUICreated = true;
        }
        SetStatListener();
    }

    public void SetStatListener()
    {
        listenerToken = realm.All<Stat>()
            .SubscribeForNotifications((sender, changes, error) =>
            {
                if (error != null)
                {
                // Show error message
                Debug.Log("an error occurred while listening for score changes :" + error);
                    return;
                }
                if (changes != null)
                {
                    SetNewlyInsertedScores(changes.InsertedIndices);
                }
            // we only need to check for inserted because scores can't be modified or deleted after the run is complete
        });
    }

    #endregion

    #region PrivateMethods
    // CreateLeaderboardUI() is a method that creates a Leaderboard title for
    // the UI and calls CreateTopStatListView() to create a list of Stat objects
    // with high scores
    private void CreateLeaderboardUI()
    {
        // create leaderboard title
        displayTitle = new Label();
        displayTitle.text = "Leaderboard:";
        displayTitle.AddToClassList("display-title");

        topStats = realm.All<Stat>().OrderByDescending(s => s.Score).ToList();
        CreateTopStatListView();
    }

    // CreateTopStatListView() is a method that creates a set of Labels containing high stats
    private void CreateTopStatListView()
    {
        int maximumAmountOfTopStats;
        // set the maximumAmountOfTopStats to 5 or less
        if (topStats.Count > 4)
        {
            maximumAmountOfTopStats = 5;
        }
        else
        {
            maximumAmountOfTopStats = topStats.Count;
        }


        var topStatsListItems = new List<string>();

        topStatsListItems.Add("Your top points: " + GetRealmPlayerTopStat());


        for (int i = 0; i < maximumAmountOfTopStats; i++)
        {
            if (topStats[i].Score > 1) // only display the top stats if they are greater than 0, and show no top stats if there are none greater than 0
            {
                topStatsListItems.Add($"{topStats[i].StatOwner.Name}: {topStats[i].Score} points");
            }
        };
        // Create a new label for each top score
        var label = new Label();
        label.AddToClassList("list-item-game-name-label");
        Func<VisualElement> makeItem = () => new Label();

        // Bind Stats to the UI
        Action<VisualElement, int> bindItem = (e, i) =>
        {
            (e as Label).text = topStatsListItems[i];
            (e as Label).AddToClassList("list-item-game-name-label");
        };

        // Provide the list view with an explict height for every row
        // so it can calculate how many items to actually display
        const int itemHeight = 5;

        listView = new ListView(topStatsListItems, itemHeight, makeItem, bindItem);
        listView.AddToClassList("list-view");
    }

    // GetRealmPlayerTopStat() is a method that queries a realm for the player's Stat object with the highest score
    private int GetRealmPlayerTopStat()
    {
        var realmPlayer = realm.All<Player>().Where(p => p.Name == username).First();
        var realmPlayerTopStat = realmPlayer.Stats.OrderByDescending(s => s.Score).First().Score;
        return realmPlayer.Stats.OrderByDescending(s => s.Score).First().Score;
    }

    private void SetNewlyInsertedScores(int[] insertedIndices)
    {
        foreach (var i in insertedIndices)
        {
            var newStat = realm.All<Stat>().ElementAt(i);
            for (var scoreIndex = 0; scoreIndex < topStats.Count; scoreIndex++)
            {
                if (topStats.ElementAt(scoreIndex).IsValid == true && topStats.ElementAt(scoreIndex).Score < newStat.Score)
                {
                    if (topStats.Count > 4)
                    {   // An item shouldn't be removed if the leaderboard has less than 5 items
                        topStats.RemoveAt(topStats.Count - 1);
                    }
                    topStats.Insert(scoreIndex, newStat);
                    root.Remove(listView); // remove the old listView
                    CreateTopStatListView(); // create a new listView
                    root.Add(listView); // add the new listView to the UI
                    break;
                }
            }
        }
    }


    #endregion

    #region UnityLifecycleMethods
    private void Awake()
    {
        Instance = this;
    }
    private void OnDisable()
    {
        if (listenerToken != null)
        {
            listenerToken.Dispose();
        }
    }

    #endregion










}