using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;


[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MittFrenzyGame : UdonSharpBehaviour
{
    // Serialize fields
    [SerializeField] private GameObject _mittPrefab;
    [SerializeField] private Transform _mittContainer;
    // TODO: Add EffectManager here?
    [SerializeField] private float _gameTime = 20f;
    [SerializeField] private StartButton _startButton;
    [SerializeField] private TextMeshProUGUI _timeText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _currentPlayerText;
    [SerializeField] private GameObject _glove0;
    [SerializeField] private GameObject _glove1;
    [SerializeField] private GameObject _mitt0;
    [SerializeField] private GameObject _mitt1;
    [SerializeField] private GameObject _mitt2;
    [SerializeField] private GameObject _mitt3;
    [SerializeField] private GameObject _mitt4;
    [SerializeField] private GameObject _mitt5;

    // Serialize fields(constant)
    [SerializeField] private float DefaultGloveScale = 0.1f;
    [SerializeField] private float DefaultEyeHeight = 1.45f;
    [SerializeField] private Vector3 DefaultGlove0Position = new Vector3 (0f, 1f, 1f);
    [SerializeField] private Vector3 DefaultGlove1Position = new Vector3 (-0.3f, 1f, 1f);

    /*
        Network Synchronization Overview:
        1. Current Player
        - [UdonSynced] private int _currentPlayerId
        - private VRCPlayerApi _currentPlayer  // VRCPlayerApi cannot be synchronized/serialized
        Note: For non-owners, _currentPlayer (VRCPlayerApi) is updated in OnDeserialization()
        when the synced _currentPlayerId changes.

        2. Score Information
        - [UdonSynced] private int _score
        - [UdonSynced] private float _totalReactionTime
        - [UdonSynced] private float _finalScore

        3. Time Management
        - [UdonSynced] private float _remainingTime

        4. Game State
        - [UdonSynced] private bool _isGameActive

        5. Active Mitt Pattern
        - [UdonSynced] private int _currentMittPatternIndex
        - [UdonSynced] private int[] _activeMittIndices
    */

    // UdonSynced variables
    [UdonSynced] private int _currentPlayerId = -1;
    [UdonSynced] private int _score = 0;
    [UdonSynced] private float _finalScore = -1f;
    [UdonSynced] private float _totalReactionTime = 0f;
    [UdonSynced] private float _remainingTime = 0f;
    [UdonSynced] private bool _isGameActive = false;
    [UdonSynced] private int _currentMittPatternIndex = -1;
    [UdonSynced] private int[] _activeMittIndices = new int[2] { -1, -1 };  // Hardcoded to length 2

    // Non-UdonSynced variables
    private VRCPlayerApi _currentPlayer = null;  // Owner of the game object
    private float _currentPlayersEyeHeight = 0f;

    private float _mittPatternStartTime = 0f;
    private int _previousScore = 0;
    private int _previousMittPatternIndex = -1;
    private int[] _previousActiveMittIndices = new int[2] { -1, -1 };
    private bool _previousIsGameActive = false;
    private float _gloveScale = 0.1f;
    private Mitt[] _mitts = new Mitt[6];

    // 0 1 2
    // 3 4 5
    private int[][] _mittPatterns = new int[][]
    {
        new int[] { 0 },  // 顔左
        new int[] { 1 },  // 顔正面
        new int[] { 0, 2 },  // 顔左右二枚
        new int[] { 2 },  // 顔右
        new int[] { 3 },  // 胴左
        new int[] { 4 },  // 胴中央
        new int[] { 5 },  // 胴右
    };

    // Default local positions of mitts (relative to mitt container)
    // DEPRECATED: Now replaced by SerializeField mitt references
    private Vector3[] _defaultMittPositions = new Vector3[]
    {
        new Vector3(-0.3f, 0.15f, 0f),
        new Vector3(0f, 0.15f, 0f),
        new Vector3(0.3f, 0.15f, 0f),
        new Vector3(-0.3f, -0.15f, 0f),
        new Vector3(0f, -0.15f, 0f),
        new Vector3(0.3f, -0.15f, 0f),
    };

    void Start()
    {
        ValidateComponents();
        InitializeGloves();
        InitializeMitts();
        InitializeUI();

        Debug.Log("[MittFrenzyGame] Initialization complete");
    }

    private void ValidateComponents()
    {
        if (_mittPrefab == null)
        {
            Debug.LogError("[MittFrenzyGame] Mitt prefab is null");
            return;
        }

        if (_mittContainer == null)
        {
            Debug.LogError("[MittFrenzyGame] Mitt container is null");
            return;
        }
    }

    private void InitializeGloves()
    {
        Debug.Log("[MittFrenzyGame] Initializing gloves");

        foreach (GameObject glove in new GameObject[] { _glove0, _glove1 })
        {
            if (glove == null)
            {
                Debug.LogError("[MittFrenzyGame] Glove object is null");
                return;
            }

            if (glove.GetComponent<Collider>() == null)
            {
                Debug.LogError("[MittFrenzyGame] Collider is missing on glove");
                return;
            }

            if (glove.GetComponent<Rigidbody>() == null)
            {
                Debug.LogError("[MittFrenzyGame] Rigidbody is missing on glove");
                return;
            }
            else
            {
                Rigidbody rb = glove.GetComponent<Rigidbody>();
                if (!rb.isKinematic)
                {
                    rb.isKinematic = true;
                    Debug.LogWarning("[MittFrenzyGame] Rigidbody is not kinematic on glove, setting to kinematic");
                }
            }

            Networking.SetOwner(Networking.LocalPlayer, glove);
            glove.SetActive(true);
        }

        Debug.Log("[MittFrenzyGame] Gloves initialized");
    }

    private void InitializeMitts()
    {
        Debug.Log("[MittFrenzyGame] Initializing mitts");
        // This gameobject is the parent of mittcontainer
        _mittContainer.transform.SetParent(this.gameObject.transform);

        _mitts = new Mitt[6] {
            _mitt0.GetComponent<Mitt>(),
            _mitt1.GetComponent<Mitt>(),
            _mitt2.GetComponent<Mitt>(),
            _mitt3.GetComponent<Mitt>(),
            _mitt4.GetComponent<Mitt>(),
            _mitt5.GetComponent<Mitt>(),
        };

        Vector3 mittsCenter = (_mitt0.transform.position + _mitt1.transform.position + _mitt2.transform.position +
                                  _mitt3.transform.position + _mitt4.transform.position + _mitt5.transform.position) / 6f;
        Vector3 offset = mittsCenter - _mittContainer.transform.position;
        _mittContainer.transform.position += offset;

        for (int i = 0; i < 6; i++) {
            GameObject mittObject = _mitts[i].gameObject;
            mittObject.transform.SetParent(_mittContainer);  // just in case
            // mittObject.transform.localPosition = _defaultMittPositions[i];
            mittObject.transform.position -= offset;
            mittObject.SetActive(false);

            _mitts[i].Initialize(this, i);

            Debug.Log($"[MittFrenzyGame] Mitt {i} initialized");
        }

        Debug.Log("[MittFrenzyGame] Mitt initialization complete");
    }

    private void InitializeUI()
    {
        Debug.Log("[MittFrenzyGame] Initializing UI");

        if (_timeText == null)
        {
            Debug.LogError("[MittFrenzyGame] Time text is null");
            return;
        }
        if (_scoreText == null)
        {
            Debug.LogError("[MittFrenzyGame] Score text is null");
            return;
        }
        if (_currentPlayerText == null)
        {
            Debug.LogError("[MittFrenzyGame] Current player text is null");
            return;
        }

        UpdateLocalUI();

        Debug.Log("[MittFrenzyGame] UI initialization complete");
    }

    private void UpdateGloveScaleAndPosition(bool isGameActive)
    {
        if (isGameActive)
        {
            if (_currentPlayer == null) return;

            // TODO: There may be a better way to scale gloves
            _gloveScale =  _currentPlayersEyeHeight * 0.1f;
            _glove0.transform.localScale = new Vector3(_gloveScale, _gloveScale, _gloveScale);
            _glove1.transform.localScale = new Vector3(_gloveScale, _gloveScale, _gloveScale);

            // Debug.Log($"[MittFrenzyGame] Glove scale adjusted to {_gloveScale}");

            // Updating local glove positions and rotations
            // Middle finger proximal may be better than hand
            _glove1.transform.position = _currentPlayer.GetBonePosition(HumanBodyBones.LeftMiddleProximal);
            _glove0.transform.position = _currentPlayer.GetBonePosition(HumanBodyBones.RightMiddleProximal);

            _glove1.transform.rotation = _currentPlayer.GetBoneRotation(HumanBodyBones.LeftMiddleProximal);
            _glove0.transform.rotation = _currentPlayer.GetBoneRotation(HumanBodyBones.RightMiddleProximal);
        }
        else
        {
            _glove0.transform.localScale = new Vector3(DefaultGloveScale, DefaultGloveScale, DefaultGloveScale);
            _glove1.transform.localScale = new Vector3(DefaultGloveScale, DefaultGloveScale, DefaultGloveScale);

            // Reset glove positions
            _glove0.transform.position = DefaultGlove0Position;
            _glove1.transform.position = DefaultGlove1Position;

            // Reset glove rotations
            _glove0.transform.rotation = Quaternion.identity;
            _glove1.transform.rotation = Quaternion.identity;
        }
    }

    private void UpdateMittScaleAndPosition(bool isGameActive)
    {
        if (isGameActive)
        {
            // Getting world position, scale, and rotation of the game object
            Vector3 worldPosition = this.gameObject.transform.position;
            Vector3 localScale = this.gameObject.transform.localScale;
            Vector3 worldRotation = this.gameObject.transform.eulerAngles;

            float chestHeight = _currentPlayer.GetBonePosition(HumanBodyBones.Chest).y;

            _mittContainer.position = new Vector3(
                worldPosition.x,
                chestHeight,
                worldPosition.z);

            float mittScaleRatio = _currentPlayersEyeHeight / DefaultEyeHeight;

            Vector3 parentScale = this.gameObject.transform.parent != null
                ? this.gameObject.transform.parent.lossyScale
                : Vector3.one;

            _mittContainer.localScale = new Vector3(
                mittScaleRatio * localScale.x / parentScale.x,
                mittScaleRatio * localScale.y / parentScale.y,
                mittScaleRatio * localScale.z / parentScale.z);

            _mittContainer.eulerAngles = worldRotation;
        }
        else
        {
            _mittContainer.localScale = Vector3.one;
        }
    }

    // Must be public for StartButton
    public void StartGame()
    {
        Debug.Log("[MittFrenzyGame] Attempting to start game");
        if (_isGameActive)
        {
            Debug.Log("[MittFrenzyGame] Game is already active, ignoring start request");
            return;
        }

        // If player is not on the ground, ignore start request
        if (!Networking.LocalPlayer.IsPlayerGrounded())
        {
            Debug.Log("[MittFrenzyGame] Player is not grounded, ignoring start request");
            return;
        }

        _startButton.gameObject.SetActive(false);

        // Setting _currentPlayer as the owner of the game object
        // This ownership is automatically synced through VRChat's networking system.
        if (!Networking.IsOwner(this.gameObject))
        {
            Debug.Log("[MittFrenzyGame] Taking ownership of game object");
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }

        // Updating UdonSynced variables: _currentPlayerID, _score, _totalReactionTime, _isGameActive
        // Initialize
        _currentPlayerId = Networking.LocalPlayer.playerId;
        _score = 0;
        _totalReactionTime = 0f;
        _isGameActive = true;
        RequestSerialization();

        // Updating local variables
        _currentPlayer = Networking.GetOwner(this.gameObject);
        _currentPlayersEyeHeight = _currentPlayer.GetAvatarEyeHeightAsMeters();
        _remainingTime = _gameTime;

        // Updating local scale
        UpdateGloveScaleAndPosition(isGameActive: true);
        UpdateMittScaleAndPosition(isGameActive: true);
        UpdateLocalUI();

        ActivateRandomMittPattern();

        Debug.Log($"[MittFrenzyGame] Game started by player {_currentPlayer.displayName}");
    }

    public void EndGame()
    {
        if (!_isGameActive)
        {
            Debug.Log("[MittFrenzyGame] EndGame ignored: Game not active");
            return;
        }

        // This function may be called by non-current player from OnPlayerLeft()
        if (Networking.LocalPlayer != _currentPlayer)
        {
            Debug.Log("[MittFrenzyGame] EndGame ignored: Not current player");
            // return;
        }
        Debug.Log("[MittFrenzyGame] Ending game");

        // Updating all UdonSynced variables
        if (_score > 0)
        {
            _finalScore = (float)Math.Round(_totalReactionTime / _score, 2);
        }
        else
        {
            _finalScore = 0f;
        }

        _currentPlayerId = -1;
        _score = 0;
        _totalReactionTime = 0f;
        _isGameActive = false;
        _activeMittIndices = new int[2] { -1, -1 };
        _currentMittPatternIndex = -1;
        _remainingTime = 0f;
        RequestSerialization();

        // Updating local variables
        _previousScore = 0;
        _previousMittPatternIndex = -1;
        _previousActiveMittIndices = new int[2] { -1, -1 };
        _previousIsGameActive = false;
        _currentPlayer = null;

        // Updating local variables in mitt objects
        foreach (Mitt mitt in _mitts)
        {
            mitt.EndCooldown();
        }

        // Updating local views
        UpdateLocalUI();  // Final score shown here
        DeactivateAllMittsView();
        UpdateGloveScaleAndPosition(isGameActive: false);
        UpdateMittScaleAndPosition(isGameActive: false);
        _glove0.transform.position = DefaultGlove0Position;
        _glove1.transform.position = DefaultGlove1Position;
        _startButton.gameObject.SetActive(true);

        Debug.Log($"[MittFrenzyGame] Game ended. Final score: {_score}");
    }

    public void Update()
    {
        // This function is called every frame
        if (!_isGameActive) return;

        if (Networking.LocalPlayer == _currentPlayer)
        {
            // Updating UdonSynced variable: _remainingTime
            _remainingTime -= Time.deltaTime;
            RequestSerialization();

            UpdateLocalUI();

            if (_remainingTime <= 0)
            {
                Debug.Log("[MittFrenzyGame] Time's up, ending game");
                EndGame();
            }
        }
    }

    public override void PostLateUpdate()
    {
        base.PostLateUpdate();

        if (_isGameActive)
        {
            UpdateGloveScaleAndPosition(isGameActive: _isGameActive);
        }
    }

    public override void OnDeserialization()
    {
        base.OnDeserialization();
        // Always update currentPlayer. _currentPlayerID is synced, but _currentPlayer is not.
        _currentPlayer = _currentPlayerId != -1 ? VRCPlayerApi.GetPlayerById(_currentPlayerId) : null;

        if (Networking.LocalPlayer == _currentPlayer) return;

        // For UdonSynced variable: _isGameActive
        if (_isGameActive != _previousIsGameActive)
        {
            if (_isGameActive)
            {
                _currentPlayersEyeHeight = _currentPlayer.GetAvatarEyeHeightAsMeters();
                _startButton.gameObject.SetActive(false);
                UpdateGloveScaleAndPosition(isGameActive: _isGameActive);
                UpdateMittScaleAndPosition(isGameActive: _isGameActive);
            }
            else
            {
                _startButton.gameObject.SetActive(true);
                UpdateGloveScaleAndPosition(isGameActive: _isGameActive);
                DeactivateAllMittsView();

                _previousScore = 0;
                _previousMittPatternIndex = -1;
                _previousActiveMittIndices = new int[2] { -1, -1 };
            }
            _previousIsGameActive = _isGameActive;
        }

        // For UdonSynced variable: _previousScore
        // _totalReactionTime changes at the same time
        if (_score != _previousScore)
        {
            UpdateLocalUI();
            _previousScore = _score;
        }

        // For UdonSynced variable: _activeMittIndices
        if (!AreArraysEqual(_activeMittIndices, _previousActiveMittIndices))
        {
            UpdateMittPatternVisibility(isCalledByCurrentPlayer: false);
            _previousActiveMittIndices = (int[])_activeMittIndices.Clone();
        }

        // For other score-and-time-related UdonSynced variables
        UpdateLocalUI();
    }

    private bool AreArraysEqual(int[] array1, int[] array2)
    {
        if (array1.Length != array2.Length) return false;

        for (int i = 0; i < array1.Length; i++)
        {
            if (array1[i] != array2[i]) return false;
        }

        return true;
    }

    public void OnHitMitt(int hitMittIndex)
    {
        if (!_isGameActive)
        {
            Debug.Log($"[MittFrenzyGame] Mitt hit ignored: Game not active. Mitt index: {hitMittIndex}");
            return;
        }

        if (Networking.LocalPlayer != _currentPlayer)
        {
            Debug.Log($"[MittFrenzyGame] Mitt hit ignored: Not current player. Mitt index: {hitMittIndex}");
            return;
        }

        Debug.Log($"[MittFrenzyGame] Mitt hit registered. Mitt index: {hitMittIndex}");

        // Updating UdonSynced variable: _activeMittIndices, _score, _totalReactionTime
        for (int i = 0; i < _activeMittIndices.Length; i++)
        {
            if (_activeMittIndices[i] == hitMittIndex)
            {
                _activeMittIndices[i] = -1;
                _score++;
                _totalReactionTime += Time.time - _mittPatternStartTime;
                break;
            }
        }

        RequestSerialization();

        Debug.Log($"[MittFrenzyGame] Hit confirmed. New score: {_score}, Reaction time: {Time.time - _mittPatternStartTime:F2}s");

        bool allMittsHit = true;
        foreach (int mittIndex in _activeMittIndices)
        {
            if (mittIndex != -1) allMittsHit = false;
        }

        if (allMittsHit)
        {
            Debug.Log("[MittFrenzyGame] All mitts hit, activating new pattern");
            ActivateRandomMittPattern();
        }

        // Updating local visibility
        UpdateMittPatternVisibility(isCalledByCurrentPlayer: true, hitIndex: hitMittIndex);
        UpdateLocalUI();
    }

    private void ActivateRandomMittPattern()
    {
        if (_currentPlayer != Networking.LocalPlayer)
        {
            Debug.Log("[MittFrenzyGame] ActivateRandomMittPattern ignored: Not current player");
            return;
        }

        Debug.Log("[MittFrenzyGame] Activating random mitt pattern");

        int newMittPatternIndex;
        do
        {
            newMittPatternIndex = UnityEngine.Random.Range(0, _mittPatterns.Length);
        } while (newMittPatternIndex == _previousMittPatternIndex);

        _currentMittPatternIndex = newMittPatternIndex;
        _previousMittPatternIndex = _currentMittPatternIndex;

        // Add delay before activating new pattern
        Debug.Log($"[MittFrenzyGame] Inserting delay before activating new pattern. ");
        float delay = UnityEngine.Random.Range(0.2f, 1.0f);
        SendCustomEventDelayedSeconds(nameof(ApplyNewMittPatternView), delay);

        Debug.Log($"[MittFrenzyGame] New pattern activated: {newMittPatternIndex}");
    }

    // Must be public for SendCustomEventDelayedSeconds
    // This is for current player only, updating UdonSynced variable: _activeMittIndices
    public void ApplyNewMittPatternView()
    {
        // SendCustomEventDelayedSeconds may call this function even after the game has ended!
        if (!_isGameActive) return;

        if (_currentMittPatternIndex < 0)
        {
            Debug.LogError($"[MittFrenzyGame] Invalid _currentMittPatternIndex: {_currentMittPatternIndex}");
            return;
        }

        if (Networking.LocalPlayer != _currentPlayer)
        {
            Debug.Log("[MittFrenzyGame] ApplyNewMittPatternView ignored: Not current player");
            return;
        }

        int[] currentMittPattern = _mittPatterns[_currentMittPatternIndex];

        // Updating UdonSynced variable: _activeMittIndices
        for (int i = 0; i < _activeMittIndices.Length; i++)
        {
            if (i < currentMittPattern.Length)
                _activeMittIndices[i] = currentMittPattern[i];
            else
                _activeMittIndices[i] = -1;
        }

        RequestSerialization();

        _mittPatternStartTime = Time.time;

        // Updating local visibility
        UpdateMittPatternVisibility(isCalledByCurrentPlayer: true);
    }

    private void DeactivateAllMittsView()
    {
        foreach (Mitt mitt in _mitts)
        {
            mitt.gameObject.SetActive(false);
        }
    }

    private void UpdateMittPatternVisibility(bool isCalledByCurrentPlayer = false, int hitIndex = -1, bool playEffect = false)
    {
        Debug.Log("[MittFrenzyGame] Updating mitt pattern");

        for (int i = 0; i < _mitts.Length; i++)
        {
            bool shouldBeActive = false;
            for (int j = 0; j < _activeMittIndices.Length; j++)
            {
                if (_activeMittIndices[j] == i)
                {
                    shouldBeActive = true;
                    break;
                }
            }

            if (_mitts[i].gameObject.activeSelf != shouldBeActive)
            {
                _mitts[i].gameObject.SetActive(shouldBeActive);
            }

            // Whether this mitt should become inactive because of hit or not
            if (!shouldBeActive && i == hitIndex)
            {
                if (isCalledByCurrentPlayer)
                {
                    if (playEffect)
                    {
                        _mitts[i].PlayHitEffect();
                        // Or call outer function
                    }
                }
                else
                {
                    if (playEffect)
                    {
                        // If need to play effect for other players as well
                        // _mitts[i].PlayHitEffect();
                        // Or call outer function
                    }
                }

                _mitts[i].EndCooldown();
            }

            Debug.Log($"[MittFrenzyGame] Mitt {i} set to {(shouldBeActive ? "active" : "inactive")}");
        }
    }

    private void UpdateLocalUI()
    {
        _timeText.text = _isGameActive ? $"Time: {Mathf.Max(0, _remainingTime):F1}" : "Time: --";
        _scoreText.text = _isGameActive ? $"Score: {_score}" : $"Final Score: {_finalScore}";
        _currentPlayerText.text = _currentPlayer != null ? $"Current Player: {_currentPlayer.displayName}" : "Waiting for player";
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        base.OnPlayerJoined(player);

        if (_isGameActive)
        {
            _currentPlayer = _currentPlayerId != -1 ? VRCPlayerApi.GetPlayerById(_currentPlayerId) : null;
        }
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        base.OnPlayerLeft(player);

        // Ownership is automatically transferred to another player
        if (Networking.IsOwner(this.gameObject))
        {
            if (player.playerId == _currentPlayerId)
            {
                Debug.Log("[MittFrenzyGame] Current player left, ending game");
                EndGame();
            }
        }
    }

    public bool IsGameActive()
    {
        return _isGameActive;
    }

    public VRCPlayerApi GetCurrentPlayer()
    {
        return _currentPlayer;
    }

    public GameObject[] GetGloves()
    {
        return new GameObject[] { _glove0, _glove1 };
    }
}
