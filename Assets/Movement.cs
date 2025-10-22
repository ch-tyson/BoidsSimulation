using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public bool drawCircle = false;

    float theta_scale = 0.01f;
    int size;
    float radius = 3f;
    LineRenderer lineRenderer;

    float fishCollisionSpeed = 0.7f;

    float baseAwayStrength = 1;
    float baseWithStrength = 1;
    float baseTowardsStrength = 1;

    float awayStength = 2;
    float withStrength = 2;
    float towardsStrength = 2;

    [HideInInspector] public bool moveAway = true;
    [HideInInspector] public bool moveWith = true;
    [HideInInspector] public bool moveToward = true;

    public bool inMyControl;

    [HideInInspector] public Vector2 lookDir;

    bool canTeleport = true;
    Vector2 mousePos;
    float distance;
    bool hitBox = true;

    GameObject nearest;
    GameObject[] agentsAll;

    Vector2 awayLookDir = Vector2.zero;
    Vector2 radiusLookDir = Vector2.zero;
    Vector2 towardLookDir = Vector2.zero;

    float awayLookDirMulti;
    float radiusLookDirMulti;
    float towardLookDirMulti;

    float colorChange = 0.5f;
    float maxDistanceOfOther = 3.0f;
    float xPerimeter = 14f;
    float yPerimeter = 8f;
    float rotationSpeed = 0.12f;
    float baseSpeed = 0.04f;
    float speed = 0.04f;

    float zRot;
    Vector2 moddedPos;
    float rotationAdjustment;

    private static float[] spectrumData = new float[512];
    private static float bassFreq, midFreq, highFreq;
    private static Vector2 waveCenter = Vector2.zero;
    private static float waveTime = 0f;

    float debugTimer = 1f;

    private void Start()
    {
        zRot = Random.Range(0, 360);
    }

    void Awake()
    {
        float sizeValue = (2.0f * Mathf.PI) / theta_scale;
        size = (int)sizeValue;
        size++;
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.positionCount = size;
    }

    void Update()
    {
        radius = distance;

        AudioListener.GetSpectrumData(spectrumData, 0, FFTWindow.BlackmanHarris);

        // bass affects separation, mid affects alignment, high affects cohesion
        bassFreq = GetFrequencyBand(0, 2);
        midFreq = GetFrequencyBand(2, 5);
        highFreq = GetFrequencyBand(5, 12);

        float total = bassFreq + midFreq + highFreq;
        if (total > 0.01f)
        {
            bassFreq = (bassFreq / total) * 3f;
            midFreq = (midFreq / total) * 3f;
            highFreq = (highFreq / total) * 3f;
        }

        awayStength = baseAwayStrength * Mathf.Pow(bassFreq * 10f, 3f);
        withStrength = baseWithStrength * Mathf.Pow(midFreq * 10f, 3f);
        towardsStrength = baseTowardsStrength * Mathf.Pow(highFreq * 10f, 3f);

        debugTimer += Time.deltaTime;
        if (debugTimer >= 1f) // every second, output freq and strengths
        {
            Debug.LogFormat(
                "{0}: bass={1:F3}, mid={2:F3}, high={3:F3}, away={4:F2}, with={5:F2}, toward={6:F2}, speed={7:F3}",
                gameObject.name,
                bassFreq, midFreq, highFreq,
                awayStength, withStrength, towardsStrength,
                speed
            );
            debugTimer = 0f;
        }

        waveTime += Time.deltaTime * 5f;

        if (drawCircle)
        {
            Vector3 pos;
            float theta = 0f;
            for (int i = 0; i < size; i++)
            {
                theta += (2.0f * Mathf.PI * theta_scale);
                float x = radius * Mathf.Cos(theta);
                float y = radius * Mathf.Sin(theta);
                x += transform.position.x;
                y += transform.position.y;
                pos = new Vector3(x, y, 0);
                lineRenderer.SetPosition(i, pos);
            }
        }

        HandleTeleportBounds();
        HandleAgents();
        UpdateColor();
        HandleMouse();
        UpdateSpeed();
    }

    private void FixedUpdate()
    {
        lookDir = Vector2.Lerp(
            lookDir,
            (awayLookDir.normalized * awayLookDirMulti * awayStength) +
            (radiusLookDir * radiusLookDirMulti * withStrength) +
            (towardLookDir.normalized * towardLookDirMulti * towardsStrength) +
            mousePos,
            0.15f
        );

        float angle = Mathf.Atan2(lookDir.y, lookDir.x) * Mathf.Rad2Deg - 90f;

        if (transform.InverseTransformDirection(lookDir).x > 0)
            rotationAdjustment = Quaternion.Angle(transform.rotation, Quaternion.Euler(0f, 0f, angle));
        else if (transform.InverseTransformDirection(lookDir).x < 0)
            rotationAdjustment = -Quaternion.Angle(transform.rotation, Quaternion.Euler(0f, 0f, angle));

        zRot = rotationAdjustment * rotationSpeed;
        transform.rotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, 0, zRot));

        if (distance < 0.2f && hitBox)
        {
            if (nearest != null)
                transform.position += (transform.position - nearest.transform.position) / 20 +
                                      (transform.up * speed * fishCollisionSpeed);
        }
        else
        {
            transform.position += transform.up * speed;
        }
    }

    void HandleAgents()
    {
        agentsAll = GameObject.FindGameObjectsWithTag("Agent");
        nearest = GetClosestEnemy(agentsAll);

        // reset accumulation per frame
        awayLookDir = Vector2.zero;
        radiusLookDir = Vector2.zero;
        towardLookDir = Vector2.zero;

        foreach (GameObject agent in agentsAll)
        {
            if (agent == gameObject) continue;

            float dist = (agent.transform.position - transform.position).magnitude;
            if (dist <= maxDistanceOfOther)
            {
                Vector2 dirToAgent = (Vector2)(agent.transform.position - transform.position);

                awayLookDir += dirToAgent * (maxDistanceOfOther - dist);
                radiusLookDir += agent.GetComponent<Movement>().lookDir;
                towardLookDir += (-dirToAgent).normalized;
            }
        }

        if (awayLookDir != Vector2.zero) awayLookDir.Normalize();
        if (radiusLookDir != Vector2.zero) radiusLookDir.Normalize();
        if (towardLookDir != Vector2.zero) towardLookDir.Normalize();

        if (nearest != null) distance = Vector2.Distance(transform.position, nearest.transform.position);
        else distance = 0;

        if (moveAway) awayLookDirMulti = Mathf.Clamp(1.2f - Mathf.Pow(distance * 2, 2), 0f, 1f);
        else awayLookDirMulti = 0;

        if (moveWith)
            radiusLookDirMulti = Mathf.Clamp(
                3 * Mathf.Clamp(0.5f - distance, 0f, 0.5f) +
                3 * Mathf.Clamp(distance - 0.5f, 0f, 0.5f), 0f, 1f);
        else radiusLookDirMulti = 0;

        if (moveToward) towardLookDirMulti = Mathf.Clamp(distance - 0.5f, 0f, 1f);
        else towardLookDirMulti = 0;
    }

    void HandleTeleportBounds()
    {
        if (!canTeleport) return;

        Vector2 pos = transform.position;
        if (pos.y > yPerimeter) pos.y = -yPerimeter;
        else if (pos.y < -yPerimeter) pos.y = yPerimeter;

        if (pos.x > xPerimeter) pos.x = -xPerimeter;
        else if (pos.x < -xPerimeter) pos.x = xPerimeter;

        if ((Vector2)transform.position != pos)
        {
            transform.position = pos;
            canTeleport = false;
            StartCoroutine(TeleportTimer());
        }
    }

    void UpdateColor()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null) return;

        Color targetColor;
        float distFromCenter = Vector2.Distance(transform.position, waveCenter);
        float bassPhase = (distFromCenter * 0.3f) - waveTime;
        float midPhase = (distFromCenter * 0.5f) - (waveTime * 1.2f);
        float highPhase = (distFromCenter * 0.7f) - (waveTime * 1.5f);

        float bassWave = Mathf.Sin(bassPhase) * 0.5f + 0.5f;
        float midWave = Mathf.Sin(midPhase) * 0.5f + 0.5f;
        float highWave = Mathf.Sin(highPhase) * 0.5f + 0.5f;

        float r = Mathf.Clamp01(bassFreq * bassWave);
        float g = Mathf.Clamp01(midFreq * midWave);
        float b = Mathf.Clamp01(highFreq * highWave);

        targetColor = new Color(r, g, b);

        float maxComponent = Mathf.Max(r, g, b);
        if (maxComponent > 0.1f)
        {
            targetColor = new Color(r / maxComponent, g / maxComponent, b / maxComponent) * maxComponent;
        }
        else
        {
            targetColor = new Color(0.15f, 0.15f, 0.15f);
        }

        spriteRenderer.material.color = Color.Lerp(spriteRenderer.material.color, targetColor, colorChange);
    }

    void HandleMouse()
    {
        if (inMyControl)
        {
            mousePos = (-(Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position) / 5).normalized * 5;
        }
        else if (Input.GetMouseButton(0))
        {
            mousePos = (-(Camera.main.ScreenToWorldPoint(Input.mousePosition) - transform.position) / 5).normalized;
        }
        else
        {
            mousePos = Vector2.zero;
        }
    }

    void UpdateSpeed()
    {
        float audioBoost = (bassFreq + midFreq + highFreq) / 5f;
        speed = baseSpeed * (1f + audioBoost);
    }

    // since GetSpectrumData gives 512 samples, we can divide them into 'bands' to get average amplitudes
    // this is important to get a more stable reading for different frequency ranges
    float GetFrequencyBand(int startIndex, int endIndex)
    {
        float average = 0;
        int count = 0;
        for (int i = startIndex; i < endIndex && i < spectrumData.Length; i++)
        {
            average += spectrumData[i];
            count++;
        }
        return count > 0 ? (average / count) : 0;
    }

    GameObject GetClosestEnemy(GameObject[] enemies)
    {
        GameObject tMin = null;
        float minDist = Mathf.Infinity;
        Vector3 currentPos = transform.position;
        foreach (GameObject t in enemies)
        {
            float dist = Vector3.Distance(t.transform.position, currentPos);
            if (dist < minDist && t.transform.position != transform.position)
            {
                tMin = t;
                minDist = dist;
            }
        }
        return tMin;
    }

    IEnumerator TeleportTimer()
    {
        yield return new WaitForSeconds(2);
        canTeleport = true;
    }
}
