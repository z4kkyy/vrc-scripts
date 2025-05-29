using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Mitt : UdonSharpBehaviour
{
    // Non-UdonSynced variables
    private MittFrenzyGame _gameManager;
    private int _mittIndex;
    private bool _isInCoolDown = false;
    private VRCPlayerApi _localPlayer;

    public void Initialize(MittFrenzyGame gameManager, int mittIndex)
    {
        _gameManager = gameManager;
        _mittIndex = mittIndex;
        _localPlayer = Networking.LocalPlayer;
        Networking.SetOwner(_localPlayer, this.gameObject);
    }

    void Start()
    {
        ValidateComponents();
        _localPlayer = Networking.LocalPlayer;
    }

    private void ValidateComponents()
    {
        if (GetComponent<Collider>() == null)
        {
            Debug.LogError("[Mitt] Collider is missing!");
        }
        else if (!GetComponent<Collider>().isTrigger)
        {
            Debug.LogWarning("[Mitt] Collider is not a trigger, setting to trigger");
            GetComponent<Collider>().isTrigger = true;
        }

        if (GetComponent<Rigidbody>() == null)
        {
            Debug.LogError("[Mitt] Rigidbody is missing!");
        }
        else
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (!rb.isKinematic)
            {
                rb.isKinematic = true;
                Debug.LogWarning("[Mitt] Rigidbody is not kinematic, setting to kinematic");
            }

            if (!rb.detectCollisions)
            {
                rb.detectCollisions = true;
                Debug.LogWarning("[Mitt] Rigidbody is not detecting collisions, setting to detect collisions");
            }
        }
    }

    public override void Interact()
    {
        if (!_gameManager.IsGameActive()) return;

        if (Networking.LocalPlayer != _gameManager.GetCurrentPlayer()) return;

        if (_isInCoolDown) return;

        _isInCoolDown = true;
        _gameManager.OnHitMitt(_mittIndex);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_gameManager.IsGameActive()) return;

        if (Networking.LocalPlayer != _gameManager.GetCurrentPlayer()) return;

        if (_isInCoolDown) return;

        GameObject[] _gloves = _gameManager.GetGloves();
        if (other.gameObject != _gloves[0] && other.gameObject != _gloves[1]) return;

        // If the mitt is hit from the back, ignore the hit
        Vector3 forwardVector = this.transform.forward;
        Vector3 collisionPoint = other.ClosestPoint(this.transform.position);
        Vector3 worldNormal = forwardVector.normalized;
        Vector3 directionToCollision = (collisionPoint - this.transform.position).normalized;
        float dotProduct = Vector3.Dot(worldNormal, directionToCollision);

        if (dotProduct > 0)
        {
            Debug.Log($"[Mitt] Dot product is {dotProduct}, ignoring hit");
            return;
        }

        _isInCoolDown = true;
        _gameManager.OnHitMitt(_mittIndex);
    }

    public void PlayHitEffect()
    {
        // Do some stuff
    }

    public void EndCooldown()
    {
        _isInCoolDown = false;
    }

    public bool IsInCooldown()
    {
        return _isInCoolDown;
    }
}
