using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using KModkit;

public class CruelMurderScript : MonoBehaviour
{
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;
	
    public KMSelectable Screen, Submit;
    public KMSelectable[] Buttons;
    public TextMesh Mode;
    public TextMesh[] Displays;
	public AudioClip[] SFX;
	
    string[,] ManorLayout = new string[6, 6]
    {
        { "Red Tower", "Attic's Storage", "Maid's Room", "Attic Landing", "Butler's Room", "Black Tower" },
        { "Red Bedroom", "Blue Bathroom", "Blue Bedroom", "3rd Floor Landing", "Purple Bedroom", "White Bedroom" },
        { "Gold Bathroom", "Master Sitting Room", "Master Bedroom", "2nd Floor Landing", "Nursery", "Children's Bath" },
        { "Garden Room", "Kitchen", "Dining Room", "Grand Foyer", "Parlor", "Library" },
        { "Meat Locker", "Pantry", "Wine Cellar", "Basement Hall", "Exercise Room", "Furnace Room" },
        { "Vault", "The Chamber", "Armory", "Ground Floor Entrance", "Workshop", "Supply Room" }
    };

    // Master list arrays for Journal mapping
    string[] Evidences = { "Hair", "Button", "Glove", "Receipt", "Footprint", "Fingerprint", "Blood" };
    string[] Suspects = { "Fred", "Boris", "Maurice", "Portia", "Ophelia", "Harriet", "Gerald" };
    string[] Weapons = { "Knife", "Laudanum", "Noose", "Rat Poison", "Axe", "Revolver", "Syringe" };
    string[] Victims = { "Rolly", "Stella", "Nancy", "Cordelia", "Lawrence", "Gus", "Evelyn" };
    string[] Motives = { "Inheritance", "Money", "Insanity", "Anger", "Jealousy", "Jewelry", "Coverup" };
    string[] Disposals = { "Acid Usage", "Barrel Usage", "Burning", "Burying", "Mutilation", "Freezer Usage", "Trash Bag Usage" };
	
    string[] Modes = {"ROOMS", "CLUES", "THE JOURNAL"};
    int CurrentMode = 0;
    
    // --- ROOMS MODE STATE VARIABLES ---
	string[,] ChosenRooms = new string[3, 2];
	Color[,] RoomColors = new Color[3, 2];
	Color[] UnderlyingWhiteColors = new Color[6];
	bool[] HasDuplicate = new bool[3];
	int[] RoomIndices = new int[3];

	// Add these two arrays to hold the assigned rooms:
	string[] FinalSuspectRooms = new string[3];
	string[] FinalVictimRooms = new string[3];

    // --- CLUES MODE STATE VARIABLES ---
    CruelMurderPuzzle GeneratedPuzzle; 
    List<Color> GlobalClueColors = new List<Color>();
    int GlobalClueIndex = 0; 

    // --- JOURNAL MODE STATE VARIABLES ---
    int JournalPhase = 0; // 0 = Layout Sequencing, 1 = Accusation (Suspects), 2 = Accusation (Victims)
    
    // Phase 1 (Layout Sequencing) Variables
    List<int> UnsolvedRooms = new List<int>();
    int CurrentLayoutRoomID;
    int[] LayoutSelections = new int[2]; // Top and Bottom display indices
    Color CurrentLayoutColor;
    List<string> CurrentTopOptions = new List<string>();
    List<string> CurrentBottomOptions = new List<string>();

    // Phase 2 & 3 (Accusation Board) Variables
    int[] AccusationSelections = new int[3];
    string[] SubmittedSuspects = new string[3];
    string[] TrueSuspects = new string[3];
    string[] TrueVictims = new string[3];
    Color[] AccusationSuspectColors = new Color[3];
    Color[] AccusationVictimColors = new Color[3];

    // --- DISPLAY SCALING ---
    Vector3[] baseDisplayScales = new Vector3[3];

    // Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool ModuleSolved;
    
    void Awake()
    {
        moduleId = moduleIdCounter++;
        for (int a = 0; a < Buttons.Count(); a++)
        {
            int Num = a;
            Buttons[Num].OnInteract += delegate
            {
                ButtonPress(Num);
                return false;
            };
        }
        Screen.OnInteract += delegate () { ClickScreen(); return false; };
        Submit.OnInteract += delegate () { ClickSubmit(); return false; };

        for (int i = 0; i < Displays.Length; i++)
        {
            baseDisplayScales[i] = Displays[i].transform.localScale;
        }
    }
    
    void Start()
	{
		GeneratedPuzzle = CruelMurderGenerator.GeneratePuzzle();
		
		int totalClues = GeneratedPuzzle.Clues[0].Count;
		Debug.LogFormat("[Cruel Murder #{0}] Puzzle successfully generated with {1} minimal clues.", moduleId, totalClues);
		Debug.LogFormat("[Cruel Murder #{0}] ------------------------------------", moduleId);
		
		// --- LOGGING: CRIME SCENE LAYOUT ---
		Debug.LogFormat("[Cruel Murder #{0}] Crime Scene Layout:", moduleId);
		for (int r = 0; r < 6; r++)
		{
			List<string> rowStrs = new List<string>();
			for (int c = 0; c < 6; c++)
			{
				string[] items = GeneratedPuzzle.Layout[new Vector2Int(c, r)];
				string suspectItem = string.IsNullOrEmpty(items[0]) ? "---" : items[0];
				string victimItem = string.IsNullOrEmpty(items[1]) ? "---" : items[1];
				rowStrs.Add(string.Format("[{0}, {1}]", suspectItem, victimItem));
			}
			// Primitive fix: Log each row individually to guarantee it prints
			Debug.LogFormat("[Cruel Murder #{0}] {1}", moduleId, string.Join(" | ", rowStrs.ToArray()));
		}
		Debug.LogFormat("[Cruel Murder #{0}] ------------------------------------", moduleId);
		
		// --- LOGGING: THE CLUES ---
		Debug.LogFormat("[Cruel Murder #{0}] Clues:", moduleId);
		for (int i = 0; i < totalClues; i++)
		{
			// Primitive fix: Log each clue on its own line to prevent string length limits
			Debug.LogFormat("[Cruel Murder #{0}] Clue {1:00}: [{2} {3} {4}]", 
				moduleId, 
				i + 1, 
				GeneratedPuzzle.Clues[0][i], 
				GeneratedPuzzle.Clues[1][i], 
				GeneratedPuzzle.Clues[2][i]);
		}
		Debug.LogFormat("[Cruel Murder #{0}] ------------------------------------", moduleId);
		
		// Initialize States
		InitializeRoomsMode();
		
		// We calculate the final answers here now so we can log them early!
		CalculateTrueAccusationAnswers(); 

		// --- LOGGING: ROOMS NOTED ---
		Debug.LogFormat("[Cruel Murder #{0}] Rooms Noted: Evidence - {1} | Suspect - {2} | Weapon - {3} | Victim - {4} | Motive - {5} | Disposal - {6}",
			moduleId, FinalSuspectRooms[0], FinalSuspectRooms[1], FinalSuspectRooms[2], FinalVictimRooms[0], FinalVictimRooms[1], FinalVictimRooms[2]);

		// --- LOGGING: CORRECT ANSWER ---
		Debug.LogFormat("[Cruel Murder #{0}] Correct Answer: Evidence - {1} | Suspect - {2} | Weapon - {3} | Victim - {4} | Motive - {5} | Disposal - {6}",
		moduleId, TrueSuspects[0], TrueSuspects[1], TrueSuspects[2], TrueVictims[0], TrueVictims[1], TrueVictims[2]);
		Debug.LogFormat("[Cruel Murder #{0}] ------------------------------------", moduleId);

		InitializeCluesMode();
		InitializeJournalMode();
		
		Mode.text = Modes[CurrentMode];
		UpdateDisplays();
	}

    void InitializeRoomsMode()
    {
        List<string> TopRooms = new List<string>();
        List<string> MidRooms = new List<string>();
        List<string> BotRooms = new List<string>();

        for(int c = 0; c < 6; c++) 
        {
            TopRooms.Add(ManorLayout[0, c]); TopRooms.Add(ManorLayout[1, c]);
            MidRooms.Add(ManorLayout[2, c]); MidRooms.Add(ManorLayout[3, c]);
            BotRooms.Add(ManorLayout[4, c]); BotRooms.Add(ManorLayout[5, c]);
        }

        // 1. Separate the colors so we can guarantee one of each per pair
        List<Color> PrimaryColors = new List<Color> { Color.red, Color.green, Color.blue };
        List<Color> SecondaryColors = new List<Color> { Color.cyan, Color.magenta, Color.yellow };
        
        ShuffleList(PrimaryColors);
        ShuffleList(SecondaryColors);

        List<string>[] RoomCategories = { TopRooms, MidRooms, BotRooms };
        
        for (int i = 0; i < 3; i++)
        {
            ChosenRooms[i, 0] = RoomCategories[i][UnityEngine.Random.Range(0, 12)];
            ChosenRooms[i, 1] = RoomCategories[i][UnityEngine.Random.Range(0, 12)];

            // Randomize whether the left or right room receives the primary color
            bool leftIsPrimary = UnityEngine.Random.Range(0, 2) == 0;

            if (ChosenRooms[i, 0] == ChosenRooms[i, 1])
            {
                HasDuplicate[i] = true;
                RoomColors[i, 0] = Color.white;
                RoomColors[i, 1] = Color.white;
                
                // Store one primary and one secondary for the Accusation board underlying colors
                UnderlyingWhiteColors[i * 2] = PrimaryColors[i];
                UnderlyingWhiteColors[i * 2 + 1] = SecondaryColors[i];
            }
            else
            {
                HasDuplicate[i] = false;
                
                // 2. Safely assign exactly one Primary (Suspect) and one Secondary (Victim)
                RoomColors[i, 0] = leftIsPrimary ? PrimaryColors[i] : SecondaryColors[i];
                RoomColors[i, 1] = leftIsPrimary ? SecondaryColors[i] : PrimaryColors[i];
            }
        }
    }

    void InitializeCluesMode()
    {
        int totalClues = GeneratedPuzzle.Clues[0].Count;
        GlobalClueColors.Clear();
        GlobalClueIndex = 0;

        for (int j = 0; j < totalClues; j++)
        {
            if (j == 0) GlobalClueColors.Add(Color.red);
            else if (j == totalClues - 1) GlobalClueColors.Add(Color.white);
            else GlobalClueColors.Add(GetFizzBuzzColor(j + 1));
        }
    }

    void InitializeJournalMode()
    {
        JournalPhase = 0;
        UnsolvedRooms = Enumerable.Range(0, 36).ToList();
        ShuffleList(UnsolvedRooms);
        LoadNextJournalRoom();
    }

    void LoadNextJournalRoom()
    {
        if (UnsolvedRooms.Count == 0)
        {
            TransitionToAccusationBoard();
            return;
        }

        CurrentLayoutRoomID = UnsolvedRooms[0];
        LayoutSelections[0] = 0;
        LayoutSelections[1] = 0;

        int r = CurrentLayoutRoomID / 6;
        
        CurrentTopOptions.Clear();
        CurrentTopOptions.Add("-");
        if (r < 2) CurrentTopOptions.AddRange(Evidences);
        else if (r < 4) CurrentTopOptions.AddRange(Suspects);
        else CurrentTopOptions.AddRange(Weapons);

        CurrentBottomOptions.Clear();
        CurrentBottomOptions.Add("-");
        if (r < 2) CurrentBottomOptions.AddRange(Victims);
        else if (r < 4) CurrentBottomOptions.AddRange(Motives);
        else CurrentBottomOptions.AddRange(Disposals);

        CurrentLayoutColor = new Color(UnityEngine.Random.Range(0.2f, 1f), UnityEngine.Random.Range(0.2f, 1f), UnityEngine.Random.Range(0.2f, 1f));
    }

    void TransitionToAccusationBoard()
	{
		JournalPhase = 1; // Accusation: Suspect Phase
		AccusationSelections[0] = 0; AccusationSelections[1] = 0; AccusationSelections[2] = 0;
		
		// Removed CalculateTrueAccusationAnswers() from here since it runs in Start()
		Debug.LogFormat("[Cruel Murder #{0}] ------------------------------------", moduleId);
		Debug.LogFormat("[Cruel Murder #{0}] Layout Sequencing Complete. Moving to Accusation Board.", moduleId);
	}

    void CalculateTrueAccusationAnswers()
    {
        int serialMoveDir = (Bomb.GetSerialNumberNumbers().Last() % 2 == 0) ? 1 : -1;

        for (int i = 0; i < 3; i++)
        {
            string suspRoom = "";
            string victRoom = "";
            Color c0 = RoomColors[i, 0];

            if (HasDuplicate[i])
            {
                suspRoom = ChosenRooms[i, 0];
                victRoom = ChosenRooms[i, 0];
                
                // Assign from the underlying colors if white
                Color u0 = UnderlyingWhiteColors[i * 2];
                Color u1 = UnderlyingWhiteColors[i * 2 + 1];
                AccusationSuspectColors[i] = IsPrimaryColor(u0) ? u0 : u1;
                AccusationVictimColors[i] = IsSecondaryColor(u0) ? u0 : u1;
            }
            else
            {
                if (IsPrimaryColor(c0))
                {
                    suspRoom = ChosenRooms[i, 0];
                    victRoom = ChosenRooms[i, 1];
                    AccusationSuspectColors[i] = RoomColors[i, 0];
                    AccusationVictimColors[i] = RoomColors[i, 1];
                }
                else
                {
                    suspRoom = ChosenRooms[i, 1];
                    victRoom = ChosenRooms[i, 0];
                    AccusationSuspectColors[i] = RoomColors[i, 1];
                    AccusationVictimColors[i] = RoomColors[i, 0];
                }
            }

            // --- ADD THESE TWO LINES HERE ---
            FinalSuspectRooms[i] = suspRoom;
            FinalVictimRooms[i] = victRoom;
            // --------------------------------

            TrueSuspects[i] = FindClueWithSerialRule(suspRoom, 0, serialMoveDir);
            TrueVictims[i] = FindClueWithSerialRule(victRoom, 1, serialMoveDir);
        }
    }

    string FindClueWithSerialRule(string startRoom, int clueIndex, int dir)
    {
        int r = -1, c = -1;
        for (int row = 0; row < 6; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                if (ManorLayout[row, col] == startRoom)
                {
                    r = row; c = col; break;
                }
            }
        }

        int currentC = c;
        for (int iter = 0; iter < 6; iter++)
        {
            string item = GeneratedPuzzle.Layout[new Vector2Int(currentC, r)][clueIndex];
            if (!string.IsNullOrEmpty(item)) return item;
            currentC = ((currentC + dir) + 6) % 6;
        }
        return "-";
    }

    bool IsPrimaryColor(Color c) { return c == Color.red || c == Color.green || c == Color.blue; }
	bool IsSecondaryColor(Color c) { return c == Color.cyan || c == Color.magenta || c == Color.yellow; }

    void ButtonPress(int Num)
    {
        if (ModuleSolved) return;
        
        Buttons[Num].AddInteractionPunch(0.2f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[Num].transform);

        int displayIndex = Num / 2;
        bool isRightButton = (Num % 2 != 0);

        if (CurrentMode == 0) // ROOMS
        {
            if (HasDuplicate[displayIndex]) return;
            RoomIndices[displayIndex] = (RoomIndices[displayIndex] + 1) % 2;
            UpdateDisplays();
        }
        else if (CurrentMode == 1) // CLUES
        {
            int maxClues = GeneratedPuzzle.Clues[0].Count;
            if (maxClues == 0) return;
            GlobalClueIndex = isRightButton ? (GlobalClueIndex + 1) % maxClues : ((GlobalClueIndex - 1) + maxClues) % maxClues;
            UpdateDisplays();
        }
        else if (CurrentMode == 2) // THE JOURNAL
        {
            if (JournalPhase == 0) // Layout Sequencing
            {
                if (displayIndex == 1) return; // Middle buttons don't function
                int listSize = (displayIndex == 0) ? CurrentTopOptions.Count : CurrentBottomOptions.Count;
                int currentSelection = (displayIndex == 0) ? LayoutSelections[0] : LayoutSelections[1];

                currentSelection = isRightButton ? (currentSelection + 1) % listSize : ((currentSelection - 1) + listSize) % listSize;

                if (displayIndex == 0) LayoutSelections[0] = currentSelection;
                else LayoutSelections[1] = currentSelection;
            }
            else // Accusation Board (Phases 1 & 2)
            {
                int maxOptions = 7; // Evidences, Suspects, etc. have 7 options each.
                AccusationSelections[displayIndex] = isRightButton 
                    ? (AccusationSelections[displayIndex] + 1) % maxOptions 
                    : ((AccusationSelections[displayIndex] - 1) + maxOptions) % maxOptions;
            }
            UpdateDisplays();
        }
    }
	
    void ClickScreen()
    {
        if (ModuleSolved) return;
        Screen.AddInteractionPunch(0.2f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Screen.transform);

        CurrentMode = (CurrentMode + 1) % 3;
        Mode.text = Modes[CurrentMode];
        UpdateDisplays();
    }
	
    void ClickSubmit()
    {
        if (ModuleSolved || CurrentMode != 2) return;
        Submit.AddInteractionPunch(0.2f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Submit.transform);

        if (JournalPhase == 0)
        {
            int r = CurrentLayoutRoomID / 6;
            int c = CurrentLayoutRoomID % 6;
            string actualSuspect = string.IsNullOrEmpty(GeneratedPuzzle.Layout[new Vector2Int(c, r)][0]) ? "-" : GeneratedPuzzle.Layout[new Vector2Int(c, r)][0];
            string actualVictim = string.IsNullOrEmpty(GeneratedPuzzle.Layout[new Vector2Int(c, r)][1]) ? "-" : GeneratedPuzzle.Layout[new Vector2Int(c, r)][1];

            string submittedTop = CurrentTopOptions[LayoutSelections[0]];
            string submittedBot = CurrentBottomOptions[LayoutSelections[1]];
            string roomName = ManorLayout[r, c];

            // Format the user submission text for logging
            string submissionText;
            if (submittedTop == "-" && submittedBot == "-")
                submissionText = "[NONE]";
            else if (submittedTop == "-")
                submissionText = submittedBot;
            else if (submittedBot == "-")
                submissionText = submittedTop;
            else
                submissionText = submittedTop + " / " + submittedBot;

            bool isCorrect = (submittedTop == actualSuspect && submittedBot == actualVictim);
            string resultText = isCorrect ? "It's correct." : "It's not correct. The module performed a strike as a result.";

            // --- LOGGING: LAYOUT SEQUENCING SUBMISSION ---
            Debug.LogFormat("[Cruel Murder #{0}] Clues submitted on {1} - {2}. {3}", moduleId, roomName, submissionText, resultText);

            if (isCorrect)
            {
				Audio.PlaySoundAtTransform(SFX[3].name, Submit.transform);
                UnsolvedRooms.RemoveAt(0);
                LoadNextJournalRoom();
                UpdateDisplays();
            }
            else
            {
				Audio.PlaySoundAtTransform(SFX[0].name, Submit.transform);
                Module.HandleStrike();
            }
        }
        else if (JournalPhase == 1) // Submitting Suspects
        {
            SubmittedSuspects[0] = Evidences[AccusationSelections[0]];
            SubmittedSuspects[1] = Suspects[AccusationSelections[1]];
            SubmittedSuspects[2] = Weapons[AccusationSelections[2]];
            
            JournalPhase = 2; // Move to Victim Clues
            AccusationSelections[0] = 0; AccusationSelections[1] = 0; AccusationSelections[2] = 0;
            UpdateDisplays();
        }
        else if (JournalPhase == 2) // Final Submission
        {
            string[] submittedVictims = new string[] {
                Victims[AccusationSelections[0]], 
                Motives[AccusationSelections[1]], 
                Disposals[AccusationSelections[2]]
            };

            // --- LOGGING: ACCUSATION BOARD SUBMISSION ---
            Debug.LogFormat("[Cruel Murder #{0}] Given Answer: Evidence - {1} | Suspect - {2} | Weapon - {3} | Victim - {4} | Motive - {5} | Disposal - {6}",
                moduleId, SubmittedSuspects[0], SubmittedSuspects[1], SubmittedSuspects[2], submittedVictims[0], submittedVictims[1], submittedVictims[2]);

            bool allCorrect = true;
            for (int i = 0; i < 3; i++)
            {
                if (SubmittedSuspects[i] != TrueSuspects[i] || submittedVictims[i] != TrueVictims[i])
                {
                    allCorrect = false;
                    break;
                }
            }

            if (allCorrect)
            {
				Audio.PlaySoundAtTransform(SFX[2].name, Submit.transform);
                Debug.LogFormat("[Cruel Murder #{0}] The clues given were correct. The case has been solved for good.", moduleId);
                Module.HandlePass();
                ModuleSolved = true;
            }
            else
            {
				Audio.PlaySoundAtTransform(SFX[1].name, Submit.transform);
                Debug.LogFormat("[Cruel Murder #{0}] The clues given were not correct. It's alright, I believe that you can solve this case once and for all.", moduleId);
                Module.HandleStrike();
                JournalPhase = 1;
                AccusationSelections[0] = 0; AccusationSelections[1] = 0; AccusationSelections[2] = 0;
                UpdateDisplays();
            }
        }
    }

    void UpdateDisplays()
    {
        if (CurrentMode == 0) // ROOMS
        {
            for (int i = 0; i < 3; i++)
            {
                UpdateDisplayText(i, ChosenRooms[i, RoomIndices[i]]);
                Displays[i].color = RoomColors[i, RoomIndices[i]];
            }
        }
        else if (CurrentMode == 1) // CLUES
        {
            if (GeneratedPuzzle != null && GeneratedPuzzle.Clues[0].Count > 0)
            {
                UpdateDisplayText(0, GeneratedPuzzle.Clues[0][GlobalClueIndex]);
                UpdateDisplayText(1, GeneratedPuzzle.Clues[1][GlobalClueIndex]);
                UpdateDisplayText(2, GeneratedPuzzle.Clues[2][GlobalClueIndex]);

                Color currentColor = GlobalClueColors[GlobalClueIndex];
                for(int i = 0; i < 3; i++) Displays[i].color = currentColor;
            }
        }
        else if (CurrentMode == 2) // THE JOURNAL
        {
            if (JournalPhase == 0)
            {
                UpdateDisplayText(0, CurrentTopOptions[LayoutSelections[0]]);
                UpdateDisplayText(1, ManorLayout[CurrentLayoutRoomID / 6, CurrentLayoutRoomID % 6]);
                UpdateDisplayText(2, CurrentBottomOptions[LayoutSelections[1]]);
                
                for(int i = 0; i < 3; i++) Displays[i].color = CurrentLayoutColor;
            }
            else if (JournalPhase == 1)
            {
                UpdateDisplayText(0, Evidences[AccusationSelections[0]]);
                UpdateDisplayText(1, Suspects[AccusationSelections[1]]);
                UpdateDisplayText(2, Weapons[AccusationSelections[2]]);

                for(int i = 0; i < 3; i++) Displays[i].color = AccusationSuspectColors[i];
            }
            else if (JournalPhase == 2)
            {
                UpdateDisplayText(0, Victims[AccusationSelections[0]]);
                UpdateDisplayText(1, Motives[AccusationSelections[1]]);
                UpdateDisplayText(2, Disposals[AccusationSelections[2]]);

                for(int i = 0; i < 3; i++) Displays[i].color = AccusationVictimColors[i];
            }
        }
    }

    private void UpdateDisplayText(int displayIndex, string text)
    {
        Displays[displayIndex].text = text;
        int maxSafeChars = 16; 
        
        if (text.Length > maxSafeChars)
        {
            float scaleFactor = (float)maxSafeChars / text.Length;
            Displays[displayIndex].transform.localScale = baseDisplayScales[displayIndex] * scaleFactor;
        }
        else Displays[displayIndex].transform.localScale = baseDisplayScales[displayIndex];
    }

    private Color GetFizzBuzzColor(int num)
    {
        List<Color> mix = new List<Color>();
        
        // --- Existing Primes (2 to 41) ---
        if (num % 2 == 0) mix.Add(Color.blue);
        if (num % 3 == 0) mix.Add(new Color(0.8f, 0.8f, 0.0f)); // Dark Yellow
        if (num % 5 == 0) mix.Add(Color.green);
        if (num % 7 == 0) mix.Add(Color.cyan);
        if (num % 11 == 0) mix.Add(Color.magenta);
        if (num % 13 == 0) mix.Add(new Color(1f, 0.5f, 0f)); // Orange
        if (num % 17 == 0) mix.Add(new Color(0.5f, 0f, 0.5f)); // Purple
        if (num % 19 == 0) mix.Add(new Color(0.6f, 0.3f, 0f)); // Brown
        if (num % 23 == 0) mix.Add(new Color(0f, 0.5f, 0f)); // Dark Green
        if (num % 29 == 0) mix.Add(new Color(0f, 0f, 0.5f)); // Navy
        if (num % 31 == 0) mix.Add(new Color(0.5f, 0f, 0f)); // Maroon
        if (num % 37 == 0) mix.Add(new Color(0f, 0.5f, 0.5f)); // Teal
        if (num % 41 == 0) mix.Add(new Color(0.5f, 0.5f, 0f)); // Olive

        // --- New Primes (up to 70) ---
        if (num % 43 == 0) mix.Add(new Color(0.8f, 0.2f, 0.2f)); // Rust
        if (num % 47 == 0) mix.Add(new Color(0.2f, 0.6f, 0.4f)); // Sea Green
        if (num % 53 == 0) mix.Add(new Color(0.4f, 0.2f, 0.6f)); // Deep Violet
        if (num % 59 == 0) mix.Add(new Color(0.6f, 0.4f, 0.2f)); // Bronze
        if (num % 61 == 0) mix.Add(new Color(0.2f, 0.4f, 0.6f)); // Steel Blue
        if (num % 67 == 0) mix.Add(new Color(0.7f, 0.1f, 0.4f)); // Raspberry

        // Fallback for primes > 67 or edge cases: darker gray to avoid white similarity
        if (mix.Count == 0) return new Color(0.4f, 0.4f, 0.4f); 

        // Blend the colors together
        float r = 0, g = 0, b = 0;
        foreach(Color c in mix) 
        { 
            r += c.r; 
            g += c.g; 
            b += c.b; 
        }
        
        r /= mix.Count; 
        g /= mix.Count; 
        b /= mix.Count;

        // Clamp brightness to prevent multi-mixes from washing out into white/light gray
        float maxRGB = Mathf.Max(r, Mathf.Max(g, b));
        if (maxRGB > 0.8f)
        {
            float scale = 0.8f / maxRGB;
            r *= scale; 
            g *= scale; 
            b *= scale;
        }

        return new Color(r, g, b);
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIdx = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIdx];
            list[swapIdx] = temp;
        }
    }
	
	//twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"To toggle the mode screen, use the command !{0} toggle | To press the submit button, use the command !{0} submit | To press the arrows, use the command !{0} press [tl/tr/ml/mr/bl/br] (optional count: 1-50) | Arrow presses can be chained: !{0} press tl 3 mr bl 5";
    #pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        string[] parameters = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parameters.Length == 0) yield break;

        if (Regex.IsMatch(command, @"^\s*(mode|screen|toggle)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (!ModuleSolved)
            {
                Screen.OnInteract();
            }
        }
        
        else if (Regex.IsMatch(command, @"^\s*(submit|enter|accuse)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            if (!ModuleSolved)
            {
                if (CurrentMode != 2)
                {
                    yield return "sendtochaterror You can only submit in THE JOURNAL mode. The command was not processed.";
                    yield break;
                }
                Submit.OnInteract();
            }
        }
        
        else if (Regex.IsMatch(parameters[0], @"^\s*press\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(parameters[0], @"^\s*arrows?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            List<int> buttonIndexes = new List<int>();
            string[] validPositions = { "tl", "tr", "ml", "mr", "bl", "br" };

            for (int i = 1; i < parameters.Length; i++)
            {
                string p = parameters[i].ToLowerInvariant();
                int btnIdx = Array.IndexOf(validPositions, p);
                
                if (btnIdx != -1)
                {
                    int count = 1;
                    if (i + 1 < parameters.Length)
                    {
                        int parsedCount; // C# 4.0 compliant declaration
                        if (int.TryParse(parameters[i + 1], out parsedCount))
                        {
                            if (parsedCount < 1 || parsedCount > 50)
                            {
                                yield return "sendtochaterror The count for button presses must be between 1 and 50. The command was not processed.";
                                yield break;
                            }
                            count = parsedCount;
                            i++; // skip the count parameter since we processed it
                        }
                    }
                    
                    for (int c = 0; c < count; c++)
                    {
                        buttonIndexes.Add(btnIdx);
                    }
                }
                else
                {
                    yield return "sendtochaterror Button sequence contain an invalid character. The command was not processed.";
                    yield break;
                }
            }

            if (buttonIndexes.Count == 0)
            {
                yield return "sendtochaterror Invalid parameter length. The command was not processed.";
                yield break;
            }

            yield return null;
            for (int x = 0; x < buttonIndexes.Count; x++)
            {
                Buttons[buttonIndexes[x]].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        yield return null;

        while (!ModuleSolved)
        {
            // Ensure we are in THE JOURNAL mode
            if (CurrentMode != 2)
            {
                Screen.OnInteract();
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            if (JournalPhase == 0)
            {
                // Sequence the Layout
                int r = CurrentLayoutRoomID / 6;
                int c = CurrentLayoutRoomID % 6;
                string actualSuspect = string.IsNullOrEmpty(GeneratedPuzzle.Layout[new Vector2Int(c, r)][0]) ? "-" : GeneratedPuzzle.Layout[new Vector2Int(c, r)][0];
                string actualVictim = string.IsNullOrEmpty(GeneratedPuzzle.Layout[new Vector2Int(c, r)][1]) ? "-" : GeneratedPuzzle.Layout[new Vector2Int(c, r)][1];

                int targetTop = CurrentTopOptions.IndexOf(actualSuspect);
                int targetBot = CurrentBottomOptions.IndexOf(actualVictim);

                IEnumerator topNav = NavigateMenu(LayoutSelections[0], targetTop, CurrentTopOptions.Count, 0, 1);
                while (topNav.MoveNext()) yield return topNav.Current;

                IEnumerator botNav = NavigateMenu(LayoutSelections[1], targetBot, CurrentBottomOptions.Count, 4, 5);
                while (botNav.MoveNext()) yield return botNav.Current;

                Submit.OnInteract();
                yield return new WaitForSeconds(0.05f);
            }
            else if (JournalPhase == 1)
            {
                // Accusation Phase: Suspects
                int target0 = Array.IndexOf(Evidences, TrueSuspects[0]);
                int target1 = Array.IndexOf(Suspects, TrueSuspects[1]);
                int target2 = Array.IndexOf(Weapons, TrueSuspects[2]);

                IEnumerator nav0 = NavigateMenu(AccusationSelections[0], target0, 7, 0, 1);
                while (nav0.MoveNext()) yield return nav0.Current;

                IEnumerator nav1 = NavigateMenu(AccusationSelections[1], target1, 7, 2, 3);
                while (nav1.MoveNext()) yield return nav1.Current;

                IEnumerator nav2 = NavigateMenu(AccusationSelections[2], target2, 7, 4, 5);
                while (nav2.MoveNext()) yield return nav2.Current;

                Submit.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
            else if (JournalPhase == 2)
            {
                // Accusation Phase: Victims
                int target0 = Array.IndexOf(Victims, TrueVictims[0]);
                int target1 = Array.IndexOf(Motives, TrueVictims[1]);
                int target2 = Array.IndexOf(Disposals, TrueVictims[2]);

                IEnumerator nav0 = NavigateMenu(AccusationSelections[0], target0, 7, 0, 1);
                while (nav0.MoveNext()) yield return nav0.Current;

                IEnumerator nav1 = NavigateMenu(AccusationSelections[1], target1, 7, 2, 3);
                while (nav1.MoveNext()) yield return nav1.Current;

                IEnumerator nav2 = NavigateMenu(AccusationSelections[2], target2, 7, 4, 5);
                while (nav2.MoveNext()) yield return nav2.Current;

                Submit.OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    // Helper Enumerator for TP AutoSolver that finds the shortest path wrapping around menus.
    IEnumerator NavigateMenu(int currentIdx, int targetIdx, int total, int leftBtnIdx, int rightBtnIdx)
    {
        if (targetIdx == -1 || currentIdx == targetIdx) yield break;

        int fwd = (targetIdx - currentIdx + total) % total;
        int bwd = (currentIdx - targetIdx + total) % total;

        int presses = Math.Min(fwd, bwd);
        int btnIdx = (fwd <= bwd) ? rightBtnIdx : leftBtnIdx;

        for (int i = 0; i < presses; i++)
        {
            Buttons[btnIdx].OnInteract();
            yield return new WaitForSeconds(0.025f);
        }
    }
}

/// <summary>
/// A data container holding the final, player-facing puzzle state.
/// </summary>
public class CruelMurderPuzzle
{
    // A 6x6 grid mapping a coordinate to the Suspect/Victim items in that room.
    public Dictionary<Vector2Int, string[]> Layout;
    
    // 3 separate lists of text clues to be split across different module displays.
    public List<List<string>> Clues;
}

/// <summary>
/// The core puzzle generation engine. Uses a Bitboard Constraint Satisfaction Problem (CSP) 
/// solver to quickly generate and minimize logical puzzles.
/// </summary>
public static class CruelMurderGenerator
{
    // Represents a logical link between two items on the board
    private struct Clue
    {
        public int Item1; // Index 0-41
        public int Item2; // Index 0-41
        public bool IsHorizontal; // True: Same Row | False: Same Column

        public Clue(int i1, int i2, bool h)
        {
            Item1 = i1;
            Item2 = i2;
            IsHorizontal = h;
        }
    }

    // Master list of all 42 items. Their index mathematically dictates their category and zone.
    private static readonly string[] ItemNames = new string[]
    {
        // Suspect-Oriented (Categories 0, 2, 4)
        "Hair", "Button", "Glove", "Receipt", "Footprint", "Fingerprint", "Blood",       // 0: Evidence (Top)
        "Fred", "Boris", "Maurice", "Portia", "Harriet", "Gerald", "Ophelia",            // 2: Suspect (Mid)
        "Knife", "Laudanum", "Noose", "Rat Poison", "Axe", "Revolver", "Syringe",        // 4: Weapon (Bot)
        
        // Victim-Oriented (Categories 1, 3, 5)
        "Rolly", "Stella", "Nancy", "Cordelia", "Lawrence", "Gus", "Evelyn",             // 1: Victim (Top)
        "Inheritance", "Money", "Insanity", "Anger", "Jealousy", "Jewelry", "Coverup",   // 3: Motive (Mid)
        "Acid Usage", "Barrel Usage", "Burning", "Burying", "Mutilation", "Freezer Usage", "Trash Bag Usage" // 5: Disposal (Bot)
    };
    
    // Bitmasks used to prevent clues from wrapping around the edges of the 6x6 board
    private static readonly ulong COL_0_MASK;
    private static readonly ulong COL_5_MASK;

    // Static constructor builds the edge masks once when the mod loads
    static CruelMurderGenerator()
    {
        for (int r = 0; r < 6; r++)
        {
            COL_0_MASK |= (1ul << (r * 6));
            COL_5_MASK |= (1ul << (r * 6 + 5));
        }
    }

    /// <summary>
    /// The main sequence that loops until a perfect, uniquely solvable puzzle is formed.
    /// </summary>
    public static CruelMurderPuzzle GeneratePuzzle(int seed = -1)
    {
        if (seed != -1) UnityEngine.Random.InitState(seed);

        int attemptsForHardPuzzle = 0;
        int absoluteFailsafe = 0;

        while (true)
        {
            absoluteFailsafe++;
            if (absoluteFailsafe > 500)
            {
                // Fallback empty puzzle if generations fail completely
                return new CruelMurderPuzzle { Layout = new Dictionary<Vector2Int, string[]>(), Clues = new List<List<string>> { new List<string>(), new List<string>(), new List<string>() } };
            }

            // 1. Initialize Board
            int[] boardS = new int[36]; 
            int[] boardV = new int[36]; 
            for (int i = 0; i < 36; i++) { boardS[i] = -1; boardV[i] = -1; }

            FillZone(0, 0, 21, boardS, boardV);  
            FillZone(2, 7, 28, boardS, boardV);  
            FillZone(4, 14, 35, boardS, boardV); 

            // 2. Extract Clues
            List<Clue> allClues = new List<Clue>();
            for (int r = 0; r < 6; r++)
                for (int c1 = 0; c1 < 5; c1++)
                    for (int c2 = c1 + 1; c2 < 6; c2++)
                        TryExtractClues(r * 6 + c1, r * 6 + c2, true, boardS, boardV, allClues);
            
            for (int c = 0; c < 6; c++)
                for (int r1 = 0; r1 < 5; r1++)
                    for (int r2 = r1 + 1; r2 < 6; r2++)
                        TryExtractClues(r1 * 6 + c, r2 * 6 + c, false, boardS, boardV, allClues);

            Shuffle(allClues);

            // 3. Initial Validation (Avoid Deadly Patterns)
            if (!TestUnique(allClues))
                continue; 

            // --- SMART CLUE SORTING ---
            int[] initialCounts = new int[42];
            foreach (Clue c in allClues)
            {
                initialCounts[c.Item1]++;
                initialCounts[c.Item2]++;
            }

            allClues.Sort((a, b) => {
                int weightA = initialCounts[a.Item1] + initialCounts[a.Item2];
                int weightB = initialCounts[b.Item1] + initialCounts[b.Item2];
                return weightA.CompareTo(weightB);
            });

            // 4. Backward Clue Elimination
            List<Clue> minimalClues = new List<Clue>(allClues);
            for (int i = minimalClues.Count - 1; i >= 0; i--)
            {
                Clue c = minimalClues[i];
                minimalClues.RemoveAt(i);
                if (!TestUnique(minimalClues))
                {
                    minimalClues.Insert(i, c); 
                }
            }

            // 5. Process of Elimination Guarantee
            attemptsForHardPuzzle++;
            
            int[] finalClueCounts = new int[42];
            foreach (Clue c in minimalClues)
            {
                finalClueCounts[c.Item1]++;
                finalClueCounts[c.Item2]++;
            }
            
            int singleClueItems = 0;
            for (int i = 0; i < 42; i++)
            {
                if (finalClueCounts[i] == 1) singleClueItems++;
            }
            
            if (singleClueItems < 2 && attemptsForHardPuzzle < 100) 
            {
                continue; 
            }

            // 6. Format Final Output
            CruelMurderPuzzle puzzle = new CruelMurderPuzzle();
            puzzle.Layout = new Dictionary<Vector2Int, string[]>();

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    int cell = r * 6 + c;
                    string sItem = boardS[cell] != -1 ? ItemNames[boardS[cell]] : "";
                    string vItem = boardV[cell] != -1 ? ItemNames[boardV[cell]] : "";
                    puzzle.Layout.Add(new Vector2Int(c, r), new string[] { sItem, vItem });
                }
            }

            puzzle.Clues = new List<List<string>>() { new List<string>(), new List<string>(), new List<string>() };
            for (int i = 0; i < minimalClues.Count; i++)
            {
                Clue c = minimalClues[i];
                
                // List 0: The First Item
                puzzle.Clues[0].Add(ItemNames[c.Item1]);
                
                // List 1: The Connection Node (Empty = Horizontal, Filled = Vertical)
                puzzle.Clues[1].Add(c.IsHorizontal ? "\u25C7" : "\u25C6");
                
                // List 2: The Second Item
                puzzle.Clues[2].Add(ItemNames[c.Item2]);
            }

            return puzzle;
        }
    }

    private static void FillZone(int startRow, int startObjS, int startObjV, int[] boardS, int[] boardV)
    {
        int[] sCells = new int[12];
        int[] vCells = new int[12];
        int baseCell = startRow * 6;
        
        for (int i = 0; i < 12; i++)
        {
            sCells[i] = baseCell + i;
            vCells[i] = baseCell + i;
        }

        for (int i = 0; i < 7; i++)
        {
            int rS = UnityEngine.Random.Range(i, 12);
            int tempS = sCells[rS];
            sCells[rS] = sCells[i];
            sCells[i] = tempS;

            int rV = UnityEngine.Random.Range(i, 12);
            int tempV = vCells[rV];
            vCells[rV] = vCells[i];
            vCells[i] = tempV;

            boardS[sCells[i]] = startObjS + i;
            boardV[vCells[i]] = startObjV + i;
        }
    }

    private static void TryExtractClues(int cell1, int cell2, bool isHoriz, int[] boardS, int[] boardV, List<Clue> pool)
    {
        List<int> items1 = new List<int>();
        if (boardS[cell1] != -1) items1.Add(boardS[cell1]);
        if (boardV[cell1] != -1) items1.Add(boardV[cell1]);

        List<int> items2 = new List<int>();
        if (boardS[cell2] != -1) items2.Add(boardS[cell2]);
        if (boardV[cell2] != -1) items2.Add(boardV[cell2]);

        foreach (int i1 in items1)
        {
            foreach (int i2 in items2)
            {
                pool.Add(new Clue(i1, i2, isHoriz));
            }
        }
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIdx = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[swapIdx];
            list[swapIdx] = temp;
        }
    }

    private static bool TestUnique(List<Clue> clues)
    {
        ulong[] initialDomains = new ulong[42];
        for (int i = 0; i < 42; i++)
        {
            int cat = i / 7;
            if (cat == 0 || cat == 3) initialDomains[i] = 0x0000000FFFul; 
            else if (cat == 1 || cat == 4) initialDomains[i] = 0x0000FFF000ul; 
            else initialDomains[i] = 0x0FFF000000ul; 
        }

        int solutions = 0;
        ulong[,] stateStack = new ulong[43, 42]; 
        
        for (int i = 0; i < 42; i++) stateStack[0, i] = initialDomains[i];
        
        SolveRecursive(0, stateStack, clues, 2, ref solutions);
        
        return solutions == 1; 
    }

    private static void SolveRecursive(int depth, ulong[,] stateStack, List<Clue> clues, int maxSolutions, ref int solutionsFound)
    {
        for (int i = 0; i < 42; i++) stateStack[depth + 1, i] = stateStack[depth, i];
        
        if (!Propagate(depth + 1, stateStack, clues)) return;

        int unsolvedIdx = -1;
        int minDomainSize = 99;
        
        for (int i = 0; i < 42; i++)
        {
            int count = PopCount(stateStack[depth + 1, i]);
            if (count > 1 && count < minDomainSize)
            {
                minDomainSize = count;
                unsolvedIdx = i;
            }
        }

        if (unsolvedIdx == -1)
        {
            solutionsFound++; 
            return;
        }

        ulong d = stateStack[depth + 1, unsolvedIdx];
        
        for (int bit = 0; bit < 36; bit++)
        {
            if ((d & (1ul << bit)) != 0)
            {
                stateStack[depth + 1, unsolvedIdx] = (1ul << bit); 
                
                SolveRecursive(depth + 1, stateStack, clues, maxSolutions, ref solutionsFound);
                
                if (solutionsFound >= maxSolutions) return; 
                
                for (int i = 0; i < 42; i++) stateStack[depth + 1, i] = stateStack[depth, i];
            }
        }
    }

    private static bool Propagate(int depth, ulong[,] stateStack, List<Clue> clues)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            
            for (int i = 0; i < clues.Count; i++)
            {
                Clue clue = clues[i];
                ulong oldA = stateStack[depth, clue.Item1];
                ulong oldB = stateStack[depth, clue.Item2];
                
                ulong validA = 0;
                ulong validB = 0;

                if (clue.IsHorizontal)
                {
                    ulong temp = oldB & ~COL_0_MASK;
                    validA |= (temp >>= 1); temp &= ~COL_0_MASK;
                    validA |= (temp >>= 1); temp &= ~COL_0_MASK;
                    validA |= (temp >>= 1); temp &= ~COL_0_MASK;
                    validA |= (temp >>= 1); temp &= ~COL_0_MASK;
                    validA |= (temp >>= 1);
                    
                    temp = oldA & ~COL_5_MASK;
                    validB |= (temp <<= 1); temp &= ~COL_5_MASK;
                    validB |= (temp <<= 1); temp &= ~COL_5_MASK;
                    validB |= (temp <<= 1); temp &= ~COL_5_MASK;
                    validB |= (temp <<= 1); temp &= ~COL_5_MASK;
                    validB |= (temp <<= 1);
                }
                else
                {
                    ulong temp = oldB;
                    validA |= (temp >>= 6); validA |= (temp >>= 6);
                    validA |= (temp >>= 6); validA |= (temp >>= 6);
                    validA |= (temp >>= 6);
                    
                    temp = oldA;
                    validB |= (temp <<= 6); validB |= (temp <<= 6);
                    validB |= (temp <<= 6); validB |= (temp <<= 6);
                    validB |= (temp <<= 6);
                    validB &= 0xFFFFFFFFFul; 
                }

                stateStack[depth, clue.Item1] &= validA;
                stateStack[depth, clue.Item2] &= validB;

                if (stateStack[depth, clue.Item1] == 0 || stateStack[depth, clue.Item2] == 0) return false;
                if (stateStack[depth, clue.Item1] != oldA || stateStack[depth, clue.Item2] != oldB) changed = true;
            }

            ulong settledSuspects = 0;
            ulong settledVictims = 0;

            for (int i = 0; i < 42; i++)
            {
                ulong dom = stateStack[depth, i];
                if (dom != 0 && (dom & (dom - 1)) == 0) 
                {
                    if (IsSuspectOriented(i)) settledSuspects |= dom;
                    else settledVictims |= dom;
                }
            }

            for (int i = 0; i < 42; i++)
            {
                ulong old = stateStack[depth, i];
                if (old != 0 && (old & (old - 1)) != 0) 
                {
                    if (IsSuspectOriented(i)) stateStack[depth, i] &= ~settledSuspects;
                    else stateStack[depth, i] &= ~settledVictims;

                    if (stateStack[depth, i] == 0) return false;
                    if (stateStack[depth, i] != old) changed = true;
                }
            }
        }
        return true;
    }

    private static bool IsSuspectOriented(int index)
    {
        int cat = index / 7;
        return cat == 0 || cat == 2 || cat == 4;
    }

    private static int PopCount(ulong n)
    {
        int count = 0;
        while (n != 0)
        {
            count++;
            n &= (n - 1);
        }
        return count;
    }
}