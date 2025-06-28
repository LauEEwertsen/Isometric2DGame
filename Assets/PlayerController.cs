using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;
using UnityEngine.Windows;

// A behaviour that is attached to a playable
public class PlayerController : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private float speed = 5;

    private Vector2 inputVector;

    [SerializeField] InputActionReference move;

    float speedMultiplier = 1f;
    float defaultSpeedMultiplier = 1f;

    [SerializeField] private Transform visual;

    [SerializeField] private Tilemap groundTilemap;

    private void Update()
    {
        inputVector = move.action.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        // Raw input (WASD / Arrow Keys)
        Vector2 input = inputVector.normalized;

        // Convert input to isometric direction
        Vector2 isoDirection = new Vector2(
            input.x + input.y,
            (input.y - input.x) / 2f
        ).normalized;

        rb.linearVelocity = isoDirection * speed * speedMultiplier;

        CheckCurrentTile();

        //Turns the player to face the direction they're moving    
        if (isoDirection != Vector2.zero)
        {
            float angle = Mathf.Atan2(isoDirection.y, isoDirection.x) * Mathf.Rad2Deg;

            float correction = 0;

            if (isoDirection.y > 0 && isoDirection.x > 0 || isoDirection.y < 0 && isoDirection.x < 0) correction = 18.5f;
            else if (isoDirection.y < 0 && isoDirection.x > 0 || isoDirection.y > 0 && isoDirection.x < 0) correction = -18.5f;

            Vector3 currentEuler = visual.transform.eulerAngles;
            visual.rotation = Quaternion.Euler(currentEuler.x, currentEuler.y, angle + correction);
        } else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    private void CheckCurrentTile()
    {
        Vector3 worldPos = transform.position;
        Vector3Int cellPos = groundTilemap.WorldToCell(worldPos);
        TileBase tile = groundTilemap.GetTile(cellPos);

        if (tile is SpeedTile speedTile)
        {
            speedMultiplier = speedTile.speedMultiplier;
        }
        else
        {
            speedMultiplier = defaultSpeedMultiplier;
        }
    }
}