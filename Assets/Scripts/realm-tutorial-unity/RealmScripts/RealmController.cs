using UnityEngine;
using Realms;
using System.Threading.Tasks;
using Realms.Sync;
using UnityEngine.SceneManagement;
using MongoDB.Bson;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine.UI;

public class RealmController : MonoBehaviour
{
    private VisualTreeAsset leaderboardUXMLVisualTree;
    private VisualTreeAsset scoreCardUXMLVisualTree;
    private VisualTreeAsset authenticationUXMLVisualTree;

    private static Realm realm;
    private static int runTime; // total amount of time you've been playing during this playthrough/run (losing/winning resets runtime)
    private static int bonusPoints = 0; // start with 0 bonus points and at the end of the game we add bonus points based on how long you played

    private static Player currentPlayer; // the Player object for the current playthrough
    public static Stat currentStat; // the Stat object for the current playthrough

    private static App realmApp = App.Create(Constants.Realm.AppId); // (Part 2 Sync): realmApp represents the MongoDB Realm backend application
    public static User syncUser; // (Part 2 Sync): syncUser represents the realmApp's currently logged in user

    #region PublicMethods
    // CollectToken() is a method that performs a write transaction to update the current playthrough Stat object's TokensCollected count
    public static void CollectToken()
    {
        realm.Write(() =>
        {
            currentStat.TokensCollected += 1;
        });
    }

    // DefeatEnemy() is a method that performs a write transaction to update the current playthrough Stat object's enemiesDefeated count
    public static void DefeatEnemy()
    {
        realm.Write(() =>
        {
            currentStat.EnemiesDefeated += 1;
        });
    }

    // DeleteCurrentStat() is a method that performs a write transaction to delete the current playthrough Stat object and remove it from the current Player object's Stats' list
    public static void DeleteCurrentStat()
    {
        ScoreCardManager.UnRegisterListener();
        realm.Write(() =>
        {
            realm.Remove(currentStat);
            currentPlayer.Stats.Remove(currentStat); // remove the reference to the currentStat from the currentPlayer object
        });
    }

    // LogOut() is a method that logs out and reloads the scene
    public static async void LogOut()
    {
        await syncUser.LogOutAsync();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static async Task<Player> OnPressRegister(string userInput, string passInput)
    {
        await realmApp.EmailPasswordAuth.RegisterUserAsync(userInput, passInput);
        syncUser = await realmApp.LogInAsync(Credentials.EmailPassword(userInput, passInput));
        realm = await GetRealm(syncUser);
        var p1 = new Player();
        p1.Id = syncUser.Id;
        p1.Name = userInput;
        var s1 = new Stat();
        s1.StatOwner = p1;
        realm.Write(() =>
        {
            currentPlayer = realm.Add(p1);
            currentStat = realm.Add(s1);
            currentPlayer.Stats.Add(currentStat);
        });
        StartGame();
        return currentPlayer;
    }

    // PlayerWon() is a method that calculates and returns the final score for the current playthrough once the player has won the game
    public static int PlayerWon()
    {
        if (runTime <= 30) // if the game is won in less than or equal to 30 seconds, +80 bonus points
        {
            bonusPoints = 80;
        }
        else if (runTime <= 60) // if the game is won in less than or equal to 1 min, +70 bonus points
        {
            bonusPoints = 70;
        }
        else if (runTime <= 90) // if the game is won in less than or equal to 1 min 30 seconds, +60 bonus points
        {
            bonusPoints = 60;
        }
        else if (runTime <= 120) // if the game is won in less than or equal to 2 mins, +50 bonus points
        {
            bonusPoints = 50;
        }

        var finalScore = (currentStat.EnemiesDefeated + 1) * (currentStat.TokensCollected + 1) + bonusPoints;
        realm.Write(() =>
        {
            currentStat.Score = finalScore;
        });

        return finalScore;
    }

    // RestartGame() is a method that creates a new plathrough Stat object and shares this new Stat object with the ScoreCardManager to update in the UI and listen for changes to it
    public static void RestartGame()
    {
        var stat = new Stat();
        stat.StatOwner = currentPlayer;
        realm.Write(() =>
        {
            currentStat = realm.Add(stat);
            currentPlayer.Stats.Add(currentStat);
        });

        ScoreCardManager.SetCurrentStat(currentStat); // call `SetCurrentStat()` to set the current stat in the UI using ScoreCardManager
        ScoreCardManager.WatchForChangesToCurrentStats(); // call `WatchForChangesToCurrentStats()` to register a listener on the new score in the ScoreCardManager

        StartGame(); // start the game by resetting the timer and officially starting a new run/playthrough
    }

    // SetLoggedInUser() is a method that finds a Player object and creates a new Stat object for the current playthrough
    // SetLoggedInUser() takes a userInput, representing a username, as a parameter
    public static async Task<Player> SetLoggedInUser(string userInput, string passInput)
    {
        syncUser = await realmApp.LogInAsync(Credentials.EmailPassword(userInput, passInput));
        if (syncUser != null)
        {
            realm = await GetRealm(syncUser);
            Debug.Log("realm is located at " + realm.Config.DatabasePath);
            currentPlayer = realm.Find<Player>(syncUser.Id);
            if (currentPlayer != null)
            {
                var s1 = new Stat();
                s1.StatOwner = currentPlayer;
                realm.Write(() =>
                {
                    currentStat = realm.Add(s1);
                    currentPlayer.Stats.Add(currentStat);
                });
                StartGame();
            }
            else
            {
                Debug.Log("This player exists a MongoDB Realm User but not as a Realm Object, please delete the MongoDB Realm User and create one using the register button");
            }
        }
        return currentPlayer;
    }


    #endregion

    #region PrivateMethods
    private void GenerateUIObjects(GameObject canvasGameObject, string uiObjectName)
    {
        var panelSettings = EditorGUIUtility.Load("Assets/Scripts/realm-tutorial-unity/UI ToolKit/UIPanelSettings.asset");

        // create an empty GameObject
        var gameObject = new GameObject();
        // create a UI object to add to the GameObject
        var uiDocument = gameObject.AddComponent<UIDocument>();
        // attach existing panel settings to the UI Document
        uiDocument.panelSettings = (PanelSettings)panelSettings;

        // Attach Manager Scripts to interact with UI Documents and attach the UI Document's VisualTreeAsset 
        switch (uiObjectName)
        {
            case "Authentication":
                gameObject.AddComponent<AuthenticationManager>();
                uiDocument.name = "Authentication";
                uiDocument.visualTreeAsset = authenticationUXMLVisualTree;
                break;
            case "Leaderboard":
                {
                    gameObject.AddComponent<LeaderboardManager>();
                    uiDocument.name = "Leaderboard";
                    uiDocument.visualTreeAsset = leaderboardUXMLVisualTree;
                }
                break;
            case "ScoreCard":
                gameObject.AddComponent<ScoreCardManager>();
                uiDocument.name = "ScoreCard";
                uiDocument.visualTreeAsset = scoreCardUXMLVisualTree;
                break;
        }

        // Attach the UI Document as a child of the Canvas
        uiDocument.transform.parent = canvasGameObject.transform;
    }

    public static async Task<Realm> GetRealm(Realms.Sync.User loggedInUser)
    {
        var syncConfiguration = new SyncConfiguration("UnityTutorialPartition", loggedInUser);
        return await Realm.GetInstanceAsync(syncConfiguration);
    }

    // StartGame() is a method that records how long the player has been playing during the current playthrough (i.e since logging in or since last losing or winning)
    private static void StartGame()
    {
        // execute a timer every 10 second
        var myTimer = new System.Timers.Timer(10000);
        myTimer.Enabled = true;
        myTimer.Elapsed += (sender, e) => runTime += 10; // increment runTime (runTime will be used to calculate bonus points once the player wins the game)
    }

    #endregion

    #region UnityLifecycleMethods
    private void Start()
    {
        // Load UXML Assets
        leaderboardUXMLVisualTree = EditorGUIUtility.Load("Assets/Scripts/realm-tutorial-unity/UI ToolKit/Leaderboard.uxml") as VisualTreeAsset;
        scoreCardUXMLVisualTree = EditorGUIUtility.Load("Assets/Scripts/realm-tutorial-unity/UI ToolKit/ScoreCard.uxml") as VisualTreeAsset;
        authenticationUXMLVisualTree = EditorGUIUtility.Load("Assets/Scripts/realm-tutorial-unity/UI ToolKit/Authentication.uxml") as VisualTreeAsset;

        // Create canvas as a container to hold UIDocuments
        var canvasGameObject = new GameObject();
        canvasGameObject.name = "Canvas";
        var canvas = canvasGameObject.AddComponent<Canvas>();

        // Configure canvas properties
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGameObject.AddComponent<CanvasScaler>();
        canvasGameObject.AddComponent<GraphicRaycaster>();

        // Generate Authentication, Leaderboard, and Scorecard UI Objects
        GenerateUIObjects(canvasGameObject, "Authentication");
        GenerateUIObjects(canvasGameObject, "Leaderboard");
        GenerateUIObjects(canvasGameObject, "ScoreCard");
    }

    #endregion
}