package com.octopuscommunity.sdk.unity.sample;

import android.app.Activity;
import com.google.android.play.core.appupdate.AppUpdateInfo;
import com.google.android.play.core.appupdate.AppUpdateManager;
import com.google.android.play.core.appupdate.AppUpdateManagerFactory;
import com.google.android.play.core.appupdate.AppUpdateOptions;
import com.google.android.play.core.install.InstallException;
import com.google.android.play.core.install.model.AppUpdateType;

/**
 * Sample-only bridge from C# (OctopusSampleAppUpdate.cs) to Play In-App Updates. It forwards
 * Play's raw answer — availability, whether IMMEDIATE is allowed, the numeric
 * InstallErrorCode — and decides nothing: the mapping lives in C#, where an EditMode test can
 * pin it. Kept by Assets/Plugins/Android/proguard-user.txt, since only JNI reaches it.
 */
public final class OctopusSampleAppUpdate {
    /** Request code of the IMMEDIATE flow; its result is not read (C# re-checks on resume). */
    private static final int REQUEST_CODE = 0x0C70;

    /** Passed to {@link Listener#onError} when the failure carries no Play error code. */
    public static final int NO_ERROR_CODE = Integer.MIN_VALUE;

    /** Implemented in C# by an AndroidJavaProxy. Called on the Android main thread. */
    public interface Listener {
        void onResult(int availability, boolean immediateAllowed, int availableVersionCode);

        void onError(int errorCode);
    }

    private static AppUpdateInfo lastInfo;

    private OctopusSampleAppUpdate() {}

    /** Asks Play for this install's update info. Never throws; every outcome reaches the listener. */
    public static void check(final Activity activity, final Listener listener) {
        try {
            AppUpdateManager manager = AppUpdateManagerFactory.create(activity.getApplicationContext());
            manager.getAppUpdateInfo()
                    .addOnSuccessListener(info -> {
                        lastInfo = info;
                        listener.onResult(info.updateAvailability(),
                                info.isUpdateTypeAllowed(AppUpdateType.IMMEDIATE),
                                info.availableVersionCode());
                    })
                    .addOnFailureListener(error -> listener.onError(
                            error instanceof InstallException
                                    ? ((InstallException) error).getErrorCode()
                                    : NO_ERROR_CODE));
        } catch (Throwable error) {
            listener.onError(NO_ERROR_CODE);
        }
    }

    /** Starts Play's IMMEDIATE flow, reporting a refused or failed start to the listener. */
    public static void startImmediateUpdate(final Activity activity, final Listener listener) {
        final AppUpdateInfo info = lastInfo;
        if (info == null || !info.isUpdateTypeAllowed(AppUpdateType.IMMEDIATE)) {
            listener.onError(NO_ERROR_CODE);
            return;
        }
        activity.runOnUiThread(() -> {
            try {
                boolean started = AppUpdateManagerFactory.create(activity.getApplicationContext())
                        .startUpdateFlowForResult(info, activity,
                                AppUpdateOptions.defaultOptions(AppUpdateType.IMMEDIATE), REQUEST_CODE);
                if (!started) listener.onError(NO_ERROR_CODE);
            } catch (Throwable ignored) {
                listener.onError(NO_ERROR_CODE);
            }
        });
    }
}
