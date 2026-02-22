
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using System.IO;

// VIEJO SISTEMA
[Serializable]
public class Data
{
    public string name;
    public int maxBlock;
    public int BPM;
    public int offset;
    public MusicNote[] notes;
}

[Serializable]
public class LongNote
{
    public int num;
}

[Serializable]
public class MusicNote
{
    public int type;
    public int num;
    public int block;
    public int LPB;
    public LongNote longNote;
    public MusicNote[] subNotes;

    public float startTime;
    public float endTime;
    public bool isHold;
}

public class NotesManager : MonoBehaviour
{
    public static NotesManager instance;
    public int noteNum;
    private string songName;

    [SerializeField]
    public TextAsset songData;

    public List<int> LaneNum = new List<int>();
    public List<int> NoteType = new List<int>();
    public List<float> NotesTime = new List<float>();
    public List<GameObject> NotesObj = new List<GameObject>();
    public List<float> HoldEndTimes = new List<float>();
    public List<MusicNote> MusicNoteAllNotes = new List<MusicNote>();

    [SerializeField]
    private float _notesSpeed; // PURO

    [SerializeField]
    private GameObject noteObj;

    [SerializeField]
    private GameObject noteObj_Old;

    [SerializeField]
    public GameObject subNoteObj;

    [SerializeField]
    public GameObject subNoteObj_Old;

    [SerializeField]
    private GameObject notesParent;

    [SerializeField]
    public GameObject notesParent_DParent;

    [SerializeField]
    public GameObject notesParent_FParent;

    [SerializeField]
    public GameObject notesParent_JParent;

    [SerializeField]
    public GameObject notesParent_KParent;
    
    public bool gManagerIsFound = false;
    bool isImported = false;
    public GManager gManager;

    public List<float> Type3NotesTime = new List<float>();

    public ImportSettings importSettings;

    public PauseGameManager pauseGameManager;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
        pauseGameManager = UnityEngine
            .GameObject.Find("PauseGameManager")
            .GetComponent<PauseGameManager>();

        _notesSpeed = NotesSpeed; 
    }

    // Sin tener en cuenta otros valores de verificacion
    void OnEnable()
    {
        Invoke("Load", 5f);
    }
    
    private void Update()
    {
        if (isImported == false)
        {
            if (importSettings == null)
            {
                importSettings = GameObject.Find("ImportSettings").GetComponent<ImportSettings>();
            }
            else if (importSettings != null)
            {
                if (importSettings.noteSpeedImport > 0.01f)
                {
                    NotesSpeed = importSettings.noteSpeedImport;
                }
                isImported = true;
            }
        }

        if (!gManagerIsFound)
        {
            if (gManager == null)
            {
                gManager = GameObject.Find("GameManager").GetComponent<GManager>();
                gManagerIsFound = true;
            }
        }

        /* Inicializacion */
    }

    private void Load()
    {
        string jsonText = songData.text;

        try 
        {
            if (jsonText.Contains("\"ignore_this_and_do_not_delete_it\": \"Hatsune Miku\"")) 
            {
                // NUEVO SISTEMA                
            }
            else
            {
                Data oldData = JsonConvert.DeserializeObject<Data>(jsonText);
                if (oldData != null && oldData.notes != null)
                {
                    Debug.Log("no miku miku beam...");
                    useNewSystem = false;
                    CreateNotesOld(oldData); // Pasamos el objeto ya cargado
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error al cargar el archivo: " + e.Message);
        }
    }

    private void CreateNotesOld(Data inputJson)
    {
        gManager.noteSpeed = _notesSpeed;
        noteNum = inputJson.notes.Length;
        gManager.totalNotes = noteNum;
        gManager.maxScore = noteNum * 5;

        for (int i = 0; i < inputJson.notes.Length; i++)
        {
            float kankaku = 60 / (inputJson.BPM * (float)inputJson.notes[i].LPB);
            float beatSec = kankaku * (float)inputJson.notes[i].LPB;
            float time =
                (beatSec * inputJson.notes[i].num / (float)inputJson.notes[i].LPB)
                + inputJson.offset
                + 0.01f;

            inputJson.notes[i].startTime = time;
            NotesTime.Add(time);
            LaneNum.Add(inputJson.notes[i].block);
            NoteType.Add(inputJson.notes[i].type);
            float z = NotesTime[i] * _notesSpeed;

            MusicNoteAllNotes.Add(inputJson.notes[i]);
            Judge.instance.totalOfNotes += 1;

            if (inputJson.notes[i].type == 1)
            {
                HoldEndTimes.Add(0); // Agregar 0 para notas normales
                CreateNormalNoteObj(inputJson.notes[i], z);
            }
            else if (inputJson.notes[i].type == 3)
            {
                Type3NotesTime.Add(NotesTime[i]);
            }

            if (inputJson.notes[i].type == 2)
            {
                inputJson.notes[i].isHold = true;

                if (inputJson.notes[i].subNotes != null && inputJson.notes[i].subNotes.Length > 0)
                {
                    MusicNote lastSubNote = inputJson.notes[i].subNotes[
                        inputJson.notes[i].subNotes.Length - 1
                    ];
                    float endTime =
                        (beatSec * lastSubNote.num / (float)lastSubNote.LPB)
                        + inputJson.offset
                        + 0.01f;
                    inputJson.notes[i].endTime = endTime;
                    HoldEndTimes.Add(endTime);

                    CreateLongNoteObj(inputJson.notes[i], z, inputJson, i);
                }
                else
                {
                    Debug.LogWarning($"Hold note {i} has no subNotes, setting default endTime.");
                    inputJson.notes[i].endTime = inputJson.notes[i].startTime + 1f; // Asignar un valor predeterminado
                    HoldEndTimes.Add(inputJson.notes[i].endTime);
                }
            }
            else
            {
                inputJson.notes[i].isHold = false;
            }
        }
    }

    public Transform FindDeepChild(Transform parent, string childName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(parent);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            if (current.name == childName)
                return current;
            foreach (Transform child in current)
                queue.Enqueue(child);
        }
        return null;
    }

    private void CreateNormalNoteObj(MusicNote note, float z)
    {
        GameObject newNote = Instantiate(
            noteObj_Old,
            new Vector3(note.block - 1.5f, 0.55f, z),
            Quaternion.identity
        );
        SetNoteParent(newNote, note.block);
        NotesObj.Add(newNote);
    }

    private void CreateLongNoteObj(MusicNote note, float zStart, Data inputJson, int index)
    {
        if (note.longNote != null && note.num == note.longNote.num)
        {
            return;
        }

        GameObject longNoteObj = new GameObject("LongNote");
        longNoteObj.transform.position = new Vector3(note.block - 1.5f, 0.55f, zStart);
        var holdingChecker = longNoteObj.AddComponent<HoldingNoteChecker>();
        if (holdingChecker == null)
        {
            Debug.LogError("No se pudo añadir el script HoldingNoteChecker al objeto LongNote.");
        }
        SetNoteParent(longNoteObj, note.block);

        float kankaku = 60 / (inputJson.BPM * (float)note.LPB);
        float beatSec = kankaku * (float)note.LPB;

        MusicNote subNote =
            note.subNotes != null && note.subNotes.Length > 0 ? note.subNotes[0] : null;

        if (subNote != null)
        {
            float subNoteTime =
                (beatSec * subNote.num / (float)subNote.LPB) + inputJson.offset + 0.01f;
            float subNoteZ = subNoteTime * _notesSpeed; // Usar _notesSpeed aquí

            float longNoteDuration =
                subNoteTime - (beatSec * note.num / (float)note.LPB + inputJson.offset + 0.01f);

            GameObject startNoteObj = Instantiate(subNoteObj_Old, longNoteObj.transform);
            startNoteObj.name = "StartNote";
            startNoteObj.transform.localPosition = Vector3.zero;

            Transform pivot = FindDeepChild(startNoteObj.transform, "PivotLongNote");

            float resultado = (subNoteZ - zStart) * 10;
            pivot.localScale = new Vector3(1, 0.01f, resultado);

            NotesObj.Add(longNoteObj);
        }
        else
        {
            Debug.LogWarning("No se encontraron subNotas para la nota larga.");
        }
    }

    private void SetNoteParent(GameObject noteObj, int block)
    {
        switch (block)
        {
            case 0:
                noteObj.transform.SetParent(notesParent_DParent.transform);
                break;
            case 1:
                noteObj.transform.SetParent(notesParent_FParent.transform);
                break;
            case 2:
                noteObj.transform.SetParent(notesParent_JParent.transform);
                break;
            case 3:
                noteObj.transform.SetParent(notesParent_KParent.transform);
                break;
            default:
                Debug.LogWarning("Block value not recognized for note " + noteObj.name);
                break;
        }
    }
}

