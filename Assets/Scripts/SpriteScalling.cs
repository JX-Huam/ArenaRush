using System;
using System.Collections;
using UnityEngine;

public class SpriteScalling : MonoBehaviour
{
    [Header("Scale Settings")]
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.2f;
    [SerializeField] private float animationDuration = 1f;
    
    [Header("Animation Settings")]
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool playOnStart = true;

    // Events
    public event EventHandler OnScaleAnimationStart;
    public event EventHandler OnScaleAnimationEnd;

    private Vector3 originalScale;
    private Coroutine scaleCoroutine;
    private bool isAnimating = false;

    private void Awake()
    {
        // Store original scale
        originalScale = transform.localScale;
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartScaleAnimation();
        }
    }

    public void StartScaleAnimation()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        
        scaleCoroutine = StartCoroutine(ScaleAnimationLoop());
    }

    public void StopScaleAnimation()
    {
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
            scaleCoroutine = null;
        }
        
        // Reset to original scale
        transform.localScale = originalScale;
        isAnimating = false;
    }

    private IEnumerator ScaleAnimationLoop()
    {
        while (true)
        {
            // Wait for random interval
            float waitTime = 1f;
            
            yield return new WaitForSeconds(waitTime);
            
            // Perform scale animation
            yield return StartCoroutine(PerformScaleAnimation());
        }
    }

    private IEnumerator PerformScaleAnimation()
    {
        isAnimating = true;
        OnScaleAnimationStart?.Invoke(this, EventArgs.Empty);
        
        // Random target scale between min and max
        float targetScaleMultiplier = 1.2f;
        Vector3 targetScale = originalScale * targetScaleMultiplier;
        
        
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        
        // Scale to target
        while (elapsed < animationDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (animationDuration / 2f);
            float curveValue = scaleCurve.Evaluate(progress);
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            yield return null;
        }
        
        // Ensure we hit the target
        transform.localScale = targetScale;
        
        elapsed = 0f;
        startScale = targetScale;
        
        // Scale back to original
        while (elapsed < animationDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (animationDuration / 2f);
            float curveValue = scaleCurve.Evaluate(progress);
            
            transform.localScale = Vector3.Lerp(startScale, originalScale, curveValue);
            yield return null;
        }
        
        // Ensure we're back to original
        transform.localScale = originalScale;
        
        isAnimating = false;
        OnScaleAnimationEnd?.Invoke(this, EventArgs.Empty);
    }

    public void SetScaleRange(float min, float max)
    {
        minScale = Mathf.Max(0.1f, min);
        maxScale = Mathf.Max(minScale + 0.1f, max);
    }

    public void SetAnimationDuration(float duration)
    {
        animationDuration = Mathf.Max(0.1f, duration);
    }

    public bool IsAnimating()
    {
        return isAnimating;
    }

    private void OnDisable()
    {
        StopScaleAnimation();
    }
}