using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine.UI;
using System.IO;
using UnityEngine.SceneManagement;

public enum GameState
{
    Playing, GameOver, WaitingForMoveToEnd
}

public enum PlayMode
{
    ClearMergedOnes, Infinity, SpawnFive, TimeAttack
}

public class GameManager : MonoBehaviour {

    private Cell[,] Cells = new Cell[4, 4];
    private List<Cell> EmptyCells = new List<Cell>();
    private List<Cell[]> rows = new List<Cell[]>();
    private List<Cell[]> columns = new List<Cell[]>();

    [Header("Game State Text")]
    public Text GameOverText;
    public Text GameOverScoreText;
    public GameObject GameOverPanel;
    public GameObject LoadFilePanel;
    public InputField LoadFilePath;
    public Text LoadFileSource;

    [Header("Feedback UI")]
    public Text KaomojiText;
    public Text WordsLeftText;
    public Text WordsCorrectText;
    public Button ClearMergedOnesButton;
    public Button InfinityButton;
    public Button SpawnFiveButton;
    public Button TimeAttackButton;
    public Color HighlightedModeColor;
    public Text TimeCounterText;
    public float TimeGiven;
    private float timeGiven;
    private int numberOfWordsLinked;
    private int totalWordCount;
    private AudioSource audioSource;
    public AudioClip Confirm;
    public AudioClip Merge;
    public Toggle AskMeaningToggle;
    public Dropdown FilePickerDropDown;

    private static GameManager instance;
    public static GameManager Instance
    {
        get
        {
            if (instance == null)
            {
                Debug.LogError("Trying to access static instance while it has not been assigned yet");
                Debug.Break();
            }

            return instance;
        }
    }

    [Header("Game properties")]
    public GameState State;
    public PlayMode Mode;
    public bool AskEnglishMeaning = false;
    public bool UseSoundEffects = true;
    public float Timer = 60;
    [Range(0,2f)]
    public float Delay;
    public int SetCount = 4;

    //
    private bool hasMoveMade = false;
    private bool[] lineMoveComplete = new bool[4] { true,true,true,true };
    int wordIndex = 0; //current word index when generating words to show on the grid, increments in steps of 4 according to SetCount
    int shownWords = 0; //increments one by one as new words get added

    private List<SimpleWord> wordStream = new List<SimpleWord>(); //stays fixed
    private List<SimpleWord> currentSet = new List<SimpleWord>(); //Contains the first three words randomized, will add the next 3 when finished etc

    /********** CORE AND BUTTON HANDLERS ********/
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.Log("There's already a Game Manager in the scene, destroying this one.");
            Destroy(gameObject);
        }
        else
        {
            instance = this;
            //DontDestroyOnLoad(gameObject);
        }
    }

    public void NewGameButtonHandler()
    {
        ResetGame();
    }

    public void OpenLoadFilePanel()
    {
        LoadFilePanel.SetActive(true);
        LoadFileSource.text = "";
    }

    public void ToggleMeaning(Toggle change)
    {
        AskEnglishMeaning = change.isOn;

        GenerateWordStream(AnkiReader.Words);
        ResetGame();
    }

    public void ToggleAudio(Toggle change)
    {
        UseSoundEffects = change.isOn;
    }

    public void OptionSelectedFromDropdown(Dropdown dropDown)
    {
        LoadFileFromPath(dropDown.options[dropDown.value].text);

        GenerateWordStream(AnkiReader.Words);
        ResetGame();
    }

    public void ScanForFiles()
    {
        string path = Application.dataPath + "/..";
        List<string> files = Directory.GetFiles(path, "*.txt").ToList();

        List<string> filenames = new List<string>();

        foreach (var file in files)
        {
            filenames.Add(Path.GetFileName(file));
        }
        FilePickerDropDown.ClearOptions();
        FilePickerDropDown.AddOptions(filenames);
        
    }

    public void LoadSampleData()
    {
        for (int i = 0; i < FilePickerDropDown.options.Count; i++)
        {
            if(FilePickerDropDown.options[i].text == "sampledata.txt")
            {
                FilePickerDropDown.value = i;
            }
        }
    }

    public void LoadFileFromInput()
    {
        LoadFileFromPath(LoadFilePath.text);
    }

    public void LoadFileFromPath(string filepath)
    {
        string path = filepath;
        path = path.Replace('"', ' ').Trim();

        StreamReader reader = new StreamReader(path);
        string text = reader.ReadToEnd();
        reader.Close();

        AnkiReader.ParseWords(text);

        string output = "Parsed words:\n";
        bool emptyMeaning = false;

        foreach (var word in AnkiReader.Words)
        {
            output += word.Kanji + ", " + word.Hiragana + ", " + word.Meaning;
            output += "\n";

            if (word.Meaning == "")
                emptyMeaning = true;
        }

        if(emptyMeaning)
        {
            output += "No word meanings detected (3rd column in text file). Starting the game without asking them";
            AskEnglishMeaning = false;
            AskMeaningToggle.isOn = false;
            AskMeaningToggle.gameObject.SetActive(false);
        }
        else
        {
            AskMeaningToggle.gameObject.SetActive(true);
            AskMeaningToggle.isOn = true;
        }
        LoadFileSource.text = output;

        GenerateWordStream(AnkiReader.Words);
        ResetGame();
    }

    public void CloseLoadFilePanel()
    {
        LoadFilePanel.SetActive(false);
    }

    public void GameOver(string gameOverText)
    {
        State = GameState.GameOver;
        GameOverText.text = gameOverText + "\n" + LocalizationManager.Instance.GetLocalizedValue("YouScored");
        GameOverScoreText.text = numberOfWordsLinked.ToString();
        GameOverPanel.SetActive(true);
    }

    public void ResetGame()
    {
        foreach (var cell in Cells)
            cell.CellStyle = 0;

        wordIndex = 0; //increments in steps of 4 (SetCount)
        shownWords = 0; //increments one by one as new words get added
        numberOfWordsLinked = 0;
        currentSet.Clear();

        Debug.Log("> Resetting the game.");

        UpdateEmptyCells();
        
        GenerateNewCell(2);
        GenerateNewCell(8);
        GenerateNewCell(12);
        GenerateNewCell(7);

        if (Mode == PlayMode.SpawnFive)
        {
            GenerateNewCell();
            GenerateNewCell();
        }

        //reset timer (set in WordStream function
        TimeGiven = timeGiven;
        TimeCounterText.text = Mathf.RoundToInt(TimeGiven).ToString();

        GameOverPanel.SetActive(false);
        State = GameState.Playing;
    }

    public void StartClearMergeMode()
    {
        Mode = PlayMode.ClearMergedOnes;

        ClearMergedOnesButton.GetComponent<Image>().color = HighlightedModeColor;

        //reset all other colors
        InfinityButton.GetComponent<Image>().color = Color.white;
        TimeAttackButton.GetComponent<Image>().color = Color.white;
        SpawnFiveButton.GetComponent<Image>().color = Color.white;

        ResetGame();
    }

    public void StartInfinityMode()
    {
        Mode = PlayMode.Infinity;

        InfinityButton.GetComponent<Image>().color = HighlightedModeColor;

        //reset all other colors
        ClearMergedOnesButton.GetComponent<Image>().color = Color.white;
        TimeAttackButton.GetComponent<Image>().color = Color.white;
        SpawnFiveButton.GetComponent<Image>().color = Color.white;

        ResetGame();
    }

    public void StartSpawnFiveMode()
    {
        Mode = PlayMode.SpawnFive;

        SpawnFiveButton.GetComponent<Image>().color = HighlightedModeColor;

        //reset all other colors
        InfinityButton.GetComponent<Image>().color = Color.white;
        TimeAttackButton.GetComponent<Image>().color = Color.white;
        ClearMergedOnesButton.GetComponent<Image>().color = Color.white;

        ResetGame();
    }

    public void StartTimeAttackMode()
    {
        Mode = PlayMode.TimeAttack;

        SpawnFiveButton.GetComponent<Image>().color = Color.white;
        InfinityButton.GetComponent<Image>().color = Color.white;
        TimeAttackButton.GetComponent<Image>().color = HighlightedModeColor;
        ClearMergedOnesButton.GetComponent<Image>().color = Color.white;

        TimeCounterText.gameObject.SetActive(true);
        ResetGame();
    }

    void Start()
    {
        Cell[] AllCells = GameObject.FindObjectsOfType<Cell>();
        foreach (var cell in AllCells)
        {
            cell.CellStyle = 0;
            Cells[cell.RowIndex, cell.ColumnIndex] = cell;
            EmptyCells.Add(cell);
        }

        columns.Add(new Cell[] { Cells[0,0], Cells[1, 0], Cells[2, 0], Cells[3, 0] });
        columns.Add(new Cell[] { Cells[0, 1], Cells[1, 1], Cells[2, 1], Cells[3, 1] });
        columns.Add(new Cell[] { Cells[0, 2], Cells[1, 2], Cells[2, 2], Cells[3, 2] });
        columns.Add(new Cell[] { Cells[0, 3], Cells[1, 3], Cells[2, 3], Cells[3, 3] });

        rows.Add(new Cell[] { Cells[0, 0], Cells[0, 1], Cells[0, 2], Cells[0, 3] });
        rows.Add(new Cell[] { Cells[1, 0], Cells[1, 1], Cells[1, 2], Cells[1, 3] });
        rows.Add(new Cell[] { Cells[2, 0], Cells[2, 1], Cells[2, 2], Cells[2, 3] });
        rows.Add(new Cell[] { Cells[3, 0], Cells[3, 1], Cells[3, 2], Cells[3, 3] });

        //
        audioSource = GetComponent<AudioSource>();

        //
        ScanForFiles();
        LoadSampleData();

        //
        StartClearMergeMode();
        //ResetGame(); //StartClearMergeMode already contains a call to this

    }

    void Update()
    {
        if (State == GameState.GameOver)
            return;

        //UI
        WordsCorrectText.text = numberOfWordsLinked.ToString();
        WordsLeftText.text = totalWordCount.ToString(); //complete words 

        if (Mode == PlayMode.TimeAttack)
        {
            TimeGiven -= Time.deltaTime;

            TimeCounterText.text = Mathf.RoundToInt(TimeGiven).ToString();

            if (TimeGiven <= 0)
            {
                TimeGiven = timeGiven;
                GameOver(LocalizationManager.Instance.GetLocalizedValue("GameOverTimeUp"));

            }
        }
    }

    public void LoadLanguageSelectScene()
    {
       LocalizationManager.Instance.Reset();
       SceneManager.LoadScene("LanguageSelectScene");
    }

    /********** WORD HANDLING ********/

    List<Word> AddWords()
    {
        List<Word> wordList = new List<Word>();
        wordList.Add(new Word("人", "ひと", "Person"));
        wordList.Add(new Word("山", "やま", "Mountain"));
        wordList.Add(new Word("花", "はな", "Flower"));
        wordList.Add(new Word("本", "ほん", "Book"));
        wordList.Add(new Word("大きい", "おおきい", "To be big"));
        wordList.Add(new Word("行く", "いく", "To go"));
        wordList.Add(new Word("読む", "よむ", "To read"));
        wordList.Add(new Word("寝る", "ねる", "To sleep"));

        return wordList;
    }

    void GenerateWordStream(List<Word> words)
    {
        wordStream.Clear();
        TimeGiven = 0;

        int id = 0;
        foreach (var word in words)
        {
            if(word.Kanji != " ")
                wordStream.Add(new SimpleWord(word.Kanji, id));

            if (word.Hiragana != " ")
                wordStream.Add(new SimpleWord(word.Hiragana, id));


            id++;
            TimeGiven += 2;
        }

        if(AskEnglishMeaning)
        {
            foreach (var word in words)
            {
                    wordStream.Add(new SimpleWord(word.Kanji, id));
                    wordStream.Add(new SimpleWord(word.Meaning, id));

                id++;
                TimeGiven += 2;
            }
        }

        //backup that won't be modified
        timeGiven = TimeGiven;
        totalWordCount = words.Count;
        TimeCounterText.text = Mathf.RoundToInt(TimeGiven).ToString();
    }

    SimpleWord GetNextWord()
    {
        //Reset the list if we're at the end and in infinity mode
        if((Mode == PlayMode.Infinity || Mode == PlayMode.TimeAttack) && wordIndex >= wordStream.Count)
        {
            wordIndex = 0;
            currentSet.Clear();
        }

        //Working in smaller sets makes sure that when dealing with large lists of words, a related word will be spawned soon.
        if(currentSet.Count == 0)
        {
            //Take 4 (SetCount public variable) starting at the right position
            currentSet = wordStream.Skip(wordIndex).Take(SetCount).ToList();

            wordIndex += SetCount;

            if (wordIndex >= wordStream.Count)
                return null;

            //randomize
            System.Random rnd = new System.Random();
            currentSet.OrderBy((item) => rnd.Next()).ToList();

            string debugline = "New set: ";
            foreach (SimpleWord item in currentSet)
            {
                debugline += item.Word + ". ";
                
            }
            Debug.Log(debugline);

        }

        SimpleWord w = currentSet[0];
        currentSet.RemoveAt(0);
        return w;
    }

    //Todo: make sure the first two words don't spawn on the same line.
    void GenerateNewCell(int index = -1)
    {
        SimpleWord word = GetNextWord();
        if (EmptyCells.Count > 0 && word != null)
        {
            shownWords++;
            int newCellIndex = index;

            //if we don't specify a position, generate one randomly
            if(index == -1)
                newCellIndex = UnityEngine.Random.Range(0, EmptyCells.Count);

            //random
            // int randomNum = UnityEngine.Random.Range(0, 10); //0-9
            // if(randomNum == 0)
            //     EmptyCells[newNumberIndex].Number = 2;

            EmptyCells[newCellIndex].SetText(word, 2);

            //
            EmptyCells[newCellIndex].PlayAppearAnimation();
            Debug.Log("Adding " + word.Word + " on the board.");
            EmptyCells.RemoveAt(newCellIndex);
        }

        if(word == null && GetActiveCellCount() == 0)
        {
            //You won
            GameOver("You won!");
        }
    }

    private int GetActiveCellCount()
    {
        int count = 0;
        foreach (var cell in Cells)
        {
            if (cell.CellStyle > 0)
                count++;
        }

        return count;
    }

    private bool IsWon()
    {
        return false;
    }

    private void UpdateEmptyCells()
    {
        EmptyCells.Clear();
        foreach (var cell in Cells)
        {
            if (cell.CellStyle == 0)
                EmptyCells.Add(cell);
        }
    }

    /********** MOVING AND MERGING TILES ********/

    bool HasMovesLeftWhenBoardIsFull()
    {
        if (EmptyCells.Count > 0)
            return true;
        else
        {
            //check column 
            for (int i = 0; i < columns.Count; i++)
                for (int j = 0; j < rows.Count - 1; j++)
                    if (Cells[j, i].CellStyle != 0 &&  Cells[j + 1, i].CellStyle != 0 && Cells[j, i].Word.ID == Cells[j + 1, i].Word.ID)
                        return true;

            //check rows
            for (int i = 0; i < rows.Count; i++)
                for (int j = 0; j < columns.Count - 1; j++)
                    if(Cells[i,j].CellStyle != 0 && Cells[i,j+1].CellStyle != 0 && Cells[i,j].Word.ID == Cells[i,j+1].Word.ID)
                        return true;

        }
        return false;
    }

    public void Move(MoveDirection direction)
    {
        if (State == GameState.WaitingForMoveToEnd)
            return;

        hasMoveMade = false;
        ResetMergeFlags();

        if (Delay > 0)
        {
            StartCoroutine(MoveCoroutine(direction));
        }
        else
        {
            //
            for (int i = 0; i < rows.Count; i++)
            {
                switch (direction)
                {
                    case MoveDirection.Down:
                        while (MakeOneMoveUpIndex(columns[i])) { }
                        break;
                    case MoveDirection.Left:
                        while (MakeOneMoveDownIndex(rows[i])) { }
                        break;
                    case MoveDirection.Right:
                        while (MakeOneMoveUpIndex(rows[i])) { }
                        break;
                    case MoveDirection.Up:
                        while (MakeOneMoveDownIndex(columns[i])) { }
                        break;

                }
            }

            UpdateEmptyCells();
            GenerateNewCell();

            HandleGameOvers();
        }
    }


    IEnumerator MoveOneLineUpIndexCoroutine(Cell[] cells, int index)
    {
        lineMoveComplete[index] = false;
        while(MakeOneMoveUpIndex(cells))
        {
            hasMoveMade = true;
            yield return new WaitForSeconds(Delay);
        }
        lineMoveComplete[index] = true;
    }

    IEnumerator MoveOneLineDownIndexCoroutine(Cell[] cells, int index)
    {
        lineMoveComplete[index] = false;
        while (MakeOneMoveDownIndex(cells))
        {
            hasMoveMade = true;
            yield return new WaitForSeconds(Delay);
        }
        lineMoveComplete[index] = true;
    }

    IEnumerator MoveCoroutine(MoveDirection direction)
    {
        State = GameState.WaitingForMoveToEnd;

        //start moving each line with a delay
        switch (direction)
        {
            case MoveDirection.Up:
                for (int i = 0; i < columns.Count; i++)
                    StartCoroutine(MoveOneLineDownIndexCoroutine(columns[i], i));
                break;
            case MoveDirection.Left:
                for (int i = 0; i < rows.Count; i++)
                    StartCoroutine(MoveOneLineDownIndexCoroutine(rows[i], i));
                break;
            case MoveDirection.Down:
                for (int i = 0; i < columns.Count; i++)
                    StartCoroutine(MoveOneLineUpIndexCoroutine(columns[i], i));
                break;
            case MoveDirection.Right:
                for (int i = 0; i < rows.Count; i++)
                    StartCoroutine(MoveOneLineUpIndexCoroutine(rows[i], i));
                break;
            default:
                break;
        }

        while (!(lineMoveComplete[0] && lineMoveComplete[1] && lineMoveComplete[2] && lineMoveComplete[3]))
            yield return null;

        //Spawn new cells when needed
        if (hasMoveMade)
        {
            UpdateEmptyCells();

            if(UseSoundEffects)
                audioSource.PlayOneShot(Confirm);

            if (Mode == PlayMode.SpawnFive)
            {
                
                int toSpawn = Mathf.Max(0, 6 - GetActiveCellCount());
                for (int i = 0; i < toSpawn; i++)
                    GenerateNewCell();
            }
            else
            {
                    GenerateNewCell();

                if (GetActiveCellCount() == 1)
                    GenerateNewCell();

                if (GetActiveCellCount() == 2)
                    GenerateNewCell();
            }

            HandleGameOvers();
        }
        else
            Debug.Log("No move made");

        KaomojiText.fontSize = UnityEngine.Random.Range(24, 120);

        //
        State = GameState.Playing;
    }

    void HandleGameOvers()
    {
        //board full
        if (!HasMovesLeftWhenBoardIsFull())
            GameOver("Game Over");
    }

    bool MakeOneMoveDownIndex(Cell[] line)
    {
        for (int i = 0; i < line.Length -1; i++)
        {
            //Move
            if(line[i].CellStyle == 0 && line[i + 1].CellStyle != 0)
            {
                line[i].Word = line[i + 1].Word;
                line[i].CellStyle = line[i + 1].CellStyle;

                line[i + 1].CellStyle = 0;
                return true;
            }

            if (HandleMerge(line[i], line[i + 1]))
                return true;

        }
        return false;
    }

    bool MakeOneMoveUpIndex(Cell[] line)
    {
        for (int i = line.Length - 1; i > 0; i--)
        {
            //Move
            if (line[i].CellStyle == 0 && line[i - 1].CellStyle != 0)
            {
                line[i].Word = line[i - 1].Word;
                line[i].CellStyle = line[i - 1].CellStyle;
                line[i - 1].CellStyle = 0;
                return true;
            }

            if (HandleMerge(line[i], line[i - 1]))
                return true;
        }
        return false;
    }

    int failedToMergeCombo = 0;
    bool HandleMerge(Cell One, Cell Two)
    {
        if (One.Word == null || Two.Word == null)
            return false;

        int score = 0;
        //Merge (down and left moves)
        if (One.CellStyle != 0 && Two.CellStyle != 0 && One.Word.ID == Two.Word.ID && !One.HasMerged && !Two.HasMerged)
        {
            Two.CellStyle = 0;

            One.HasMerged = true;
            One.CellStyle = 0;
            score = 2;

            One.PlayMergeAnimation();

            Debug.Log("Removing " + One.Word.Word + " after merging with " + Two.Word.Word);

            //
            Two.PlayMergeAnimation();

            if (UseSoundEffects)
                audioSource.PlayOneShot(Merge);

            // 
            ScoreTracker.Instance.Score += score;

            failedToMergeCombo = 0;
            KaomojiText.text = "";

            numberOfWordsLinked++;

            return true;
        }

        failedToMergeCombo++;

        if (failedToMergeCombo > 55)
        {
            KaomojiText.text = "	( ; ω ; )";
        }
        else
            KaomojiText.text = "";

        return false;
    }

    private void ResetMergeFlags()
    {
        foreach (var cell in Cells)
            cell.HasMerged = false;
    }
}
