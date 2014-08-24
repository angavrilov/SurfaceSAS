// License: MIT

using System;
using UnityEngine;

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class SurfaceRelativeSASController : MonoBehaviour
{
    enum State {
        AUTO, ON, OFF
    };

    private static State mode = State.AUTO;
    private ApplicationLauncherButton appButtonStock;
    private bool buttonActive = false;

    Texture button_off, button_on, button_auto;

    public SurfaceRelativeSASController()
    {
        GameEvents.onGUIApplicationLauncherReady.Add(OnGUIAppLauncherReady);

        button_off = (Texture)GameDatabase.Instance.GetTexture("SurfaceSAS/icon_off", false);
        button_on = (Texture)GameDatabase.Instance.GetTexture("SurfaceSAS/icon_on", false);
        button_auto = (Texture)GameDatabase.Instance.GetTexture("SurfaceSAS/icon_auto", false);
    }

    public void OnDestroy()
    {
        GameEvents.onGUIApplicationLauncherReady.Remove(OnGUIAppLauncherReady);
        if (appButtonStock != null)
            ApplicationLauncher.Instance.RemoveModApplication(appButtonStock);
    }

    private void OnGUIAppLauncherReady()
    {
        if (ApplicationLauncher.Ready)
        {
            appButtonStock = ApplicationLauncher.Instance.AddModApplication(
                OnIconClickHandler,
                OnIconClickHandler,
                DummyVoid,
                DummyVoid,
                DummyVoid,
                DummyVoid,
                ApplicationLauncher.AppScenes.FLIGHT | ApplicationLauncher.AppScenes.MAPVIEW,
                GetButtonTexture()
            );
        }
    }

    private void DummyVoid() {}

    private void OnIconClickHandler()
    {
        SetButtonActive(buttonActive);

        switch (mode)
        {
        case State.AUTO:
            mode = State.ON;
            break;

        case State.ON:
            mode = State.OFF;
            break;

        default:
            mode = State.AUTO;
            break;
        }

        appButtonStock.SetTexture(GetButtonTexture());
    }

    private Texture GetButtonTexture()
    {
        switch (mode)
        {
        case State.AUTO:
            return button_auto;

        case State.ON:
            return button_on;

        case State.OFF:
            return button_off;

        default:
            return null;
        }
    }

    private void SetButtonActive(bool active)
    {
        buttonActive = active;

        if (appButtonStock)
        {
            if (buttonActive)
                appButtonStock.SetTrue(false);
            else
                appButtonStock.SetFalse(false);
        }
    }

    private bool ShouldUpdate(Vessel vessel)
    {
        switch (mode)
        {
        case State.AUTO:
            return vessel.situation == Vessel.Situations.FLYING ||
                   (vessel.mainBody.atmosphere &&
                    (vessel.situation == Vessel.Situations.LANDED ||
                     vessel.situation == Vessel.Situations.SPLASHED));

        case State.ON:
            return true;

        case State.OFF:
            return false;

        default:
            return false;
        }
    }

    private Vessel lastVessel;
    private CelestialBody lastBody;
    private Vector3d lastPosition;
    private Transform lastTransform;
    private bool lastActive;

    public static QuaternionD FromToRotation(Vector3d fromv, Vector3d tov)
    {
        // Doesn't handle the singularity with precisely opposite vectors
        Vector3d cross = Vector3d.Cross(fromv, tov);
        double dot = Vector3d.Dot(fromv, tov);
        double wval = dot + Math.Sqrt(fromv.sqrMagnitude * tov.sqrMagnitude);
        double norm = 1.0 / Math.Sqrt(cross.sqrMagnitude + wval*wval);
        return new QuaternionD(cross.x * norm, cross.y * norm, cross.z * norm, wval * norm);
    }

    public void FixedUpdate()
    {
        bool nextActive = false;
        bool working = false;

        Vessel active = FlightGlobals.ActiveVessel;

        if (active && !active.packed)
        {
            working = ShouldUpdate(active);

            if (lastActive && working &&
                active == lastVessel &&
                lastVessel.mainBody == lastBody &&
                lastVessel.ReferenceTransform == lastTransform &&
                !lastVessel.packed &&
                lastVessel.ActionGroups[KSPActionGroup.SAS])
            {
                Vector3d newPosition = (Vector3d)lastTransform.position - lastBody.position;
                QuaternionD delta = /*QuaternionD.*/FromToRotation(lastPosition, newPosition);
                QuaternionD adjusted = delta * (QuaternionD)lastVessel.vesselSAS.lockedHeading;
                lastVessel.vesselSAS.lockedHeading = adjusted;
            }
            else
            {
                working = false;
            }

            if (active.ActionGroups[KSPActionGroup.SAS])
            {
                lastVessel = active;
                lastBody = lastVessel.mainBody;
                lastTransform = lastVessel.ReferenceTransform;
                lastPosition = (Vector3d)lastTransform.position - lastBody.position;
                nextActive = true;
            }
        }

        lastActive = nextActive;

        if (buttonActive != working)
            SetButtonActive(working);
    }
}
