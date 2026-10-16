using System;
using UnityEngine;

/// <summary>
/// 플레이어가 현재 보유한 자동차 열쇠 ID를 관리한다.
/// 초기 구현에서는 게임 시작 시 열쇠 1개를 보유하며, 성공/실패 후에도 소비하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCarKeyInventory : MonoBehaviour
{
    [Header("Starting key")]
    [Tooltip("게임 시작 시 플레이어가 보유한 차키 ID입니다. 자동차의 Car ID와 일치하면 성공합니다.")]
    [SerializeField] private string startingKeyId = "CAR_4F_01";

    [Header("Runtime key")]
    [SerializeField] private string heldKeyId;
    [SerializeField] private bool initializeOnAwake = true;

    public string StartingKeyId => NormalizeId(startingKeyId);
    public string HeldKeyId => NormalizeId(heldKeyId);
    public bool HasAnyKey => !string.IsNullOrEmpty(HeldKeyId);

    private void Awake()
    {
        if (initializeOnAwake)
        {
            ResetForNewGame();
        }
    }

    public bool HasKeyFor(string carId)
    {
        string normalizedCarId = NormalizeId(carId);
        return HasAnyKey &&
               !string.IsNullOrEmpty(normalizedCarId) &&
               string.Equals(HeldKeyId, normalizedCarId, StringComparison.Ordinal);
    }

    public bool SetHeldKey(string keyId)
    {
        string normalizedId = NormalizeId(keyId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return false;
        }

        heldKeyId = normalizedId;
        return true;
    }

    public void ClearHeldKey()
    {
        heldKeyId = string.Empty;
    }

    public void ResetForNewGame()
    {
        heldKeyId = NormalizeId(startingKeyId);
    }

    private void OnValidate()
    {
        startingKeyId = NormalizeId(startingKeyId);
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
