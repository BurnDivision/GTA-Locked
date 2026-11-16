package com.burndivision.gtaperspectivelock;

import android.content.res.Configuration;
import android.os.Bundle;

import org.libsdl.app.SDLActivity;

/** Runs the Carnage3D engine (libmain.so) with the perspective-lock camera. */
public class GameActivity extends SDLActivity {
    private static native void nativeSetTouchMode(int mode);

    @Override
    protected String[] getLibraries() {
        return new String[] { "SDL2", "main" };
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        OrientationPolicy.apply(this);
        try {
            nativeSetTouchMode(EngineConfig.touchMode(this));
        } catch (UnsatisfiedLinkError e) {
            // native library failed to load; SDLActivity shows its own error
        }
    }

    // SDL would lock the orientation from its hint; use our screen-size based policy instead
    @Override
    public void setOrientationBis(int w, int h, boolean resizable, String hint) {
        OrientationPolicy.apply(this);
    }

    // fold / unfold changes the screen size without recreating the activity
    @Override
    public void onConfigurationChanged(Configuration newConfig) {
        super.onConfigurationChanged(newConfig);
        OrientationPolicy.apply(this);
    }
}
