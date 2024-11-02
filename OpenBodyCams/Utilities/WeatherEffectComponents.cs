using System;
using System.Linq;

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace OpenBodyCams.Utilities;

internal class WeatherEffectComponents
{
    internal LevelWeatherType weatherType;
    internal WeatherEffect effect;
    internal GameObject effectObject;
    internal Behaviour[] behaviours = [];
    internal Renderer[] renderers = [];

    internal bool enabled = false;
    internal bool active = false;
    internal Vector3 transitionPoint = Vector3.zero;
    internal int transitionFrame = -1;

    internal WeatherEffectComponents(LevelWeatherType weatherType, GameObject overrideEffectObject = null)
    {
        this.weatherType = weatherType;
        effect = TimeOfDay.Instance.effects[(int)weatherType];
        effectObject = overrideEffectObject ?? effect.effectObject;
        if (effectObject != null)
        {
            // Intentionally exclude ParticleSystem here. We do not want particles to reset when
            // the effects are hidden or shown.
            behaviours = effectObject.GetComponentsInChildren<Behaviour>().Where(b => b.enabled && (b is Light || b is LocalVolumetricFog)).ToArray();
            renderers = effectObject.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        }
    }

    internal void SetVisibility(bool show)
    {
        if (effectObject == null || !effectObject.activeInHierarchy)
            return;

        foreach (var behaviour in behaviours)
            behaviour.enabled = show;
        foreach (var renderer in renderers)
            renderer.enabled = show;
    }

    internal void Update(Transform target, float deltaTime)
    {
        void SetActive(bool value)
        {
            if (value != active)
                effectObject?.SetActive(value);
            active = value;
        }

        if (enabled)
        {
            if (effectObject != null)
            {
                if (effect.lerpPosition)
                    effectObject.transform.position = Vector3.Lerp(effectObject.transform.position, target.position, Mathf.Clamp01(deltaTime));
                else
                    effectObject.transform.position = target.position;
                SetActive(true);
            }
            transitionFrame = -1;
        }
        else
        {
            if (effectObject != null && effect.lerpPosition)
            {
                if (transitionFrame == -1)
                {
                    transitionFrame = 0;
                    transitionPoint = target.position;
                }
            }
            else
            {
                SetActive(false);
                transitionFrame = -1;
            }
        }

        if (transitionFrame >= 0)
        {
            var transitionTime = Math.Min(transitionFrame / 270f, 1);
            effectObject.transform.position = Vector3.Lerp(effectObject.transform.position, transitionPoint + Vector3.down * 50, transitionTime);
            var activeThisFrame = transitionTime < 1;
            if (activeThisFrame)
            {
                transitionFrame++;
                SetActive(true);
            }
            else
            {
                SetActive(false);
            }
        }
    }
}
