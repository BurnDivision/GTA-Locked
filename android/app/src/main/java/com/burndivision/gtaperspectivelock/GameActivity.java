package com.burndivision.gtaperspectivelock;

import org.libsdl.app.SDLActivity;

/** Runs the Carnage3D engine (libmain.so) with the perspective-lock camera. */
public class GameActivity extends SDLActivity {
    @Override
    protected String[] getLibraries() {
        return new String[] { "SDL2", "main" };
    }
}
