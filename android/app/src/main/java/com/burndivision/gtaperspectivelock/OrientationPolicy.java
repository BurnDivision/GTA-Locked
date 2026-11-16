package com.burndivision.gtaperspectivelock;

import android.app.Activity;
import android.content.pm.ActivityInfo;

/**
 * Large screens (smallest width >= 600dp, e.g. an unfolded Galaxy Z Fold) rotate freely and
 * follow the user's auto-rotate setting, including the system rotate-suggestion button.
 * Phone-sized screens (e.g. a Fold's cover screen) stay landscape.
 */
final class OrientationPolicy {
    private OrientationPolicy() {}

    static void apply(Activity activity) {
        int sw = activity.getResources().getConfiguration().smallestScreenWidthDp;
        int want = (sw >= 600) ? ActivityInfo.SCREEN_ORIENTATION_FULL_USER
                               : ActivityInfo.SCREEN_ORIENTATION_USER_LANDSCAPE;
        if (activity.getRequestedOrientation() != want) {
            activity.setRequestedOrientation(want);
        }
    }
}
