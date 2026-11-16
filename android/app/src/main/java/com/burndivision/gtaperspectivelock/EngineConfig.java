package com.burndivision.gtaperspectivelock;

import android.content.Context;
import android.util.Log;

import org.json.JSONObject;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

/** Reads and writes single keys of the engine config (files/gamedata/config/sys_config.json). */
final class EngineConfig {
    private EngineConfig() {}

    static File file(Context c) {
        return new File(c.getFilesDir(), "gamedata/config/sys_config.json");
    }

    static String get(Context c, String key, String def) {
        try {
            JSONObject o = new JSONObject(new String(Files.readAllBytes(file(c).toPath()), StandardCharsets.UTF_8));
            return o.has(key) ? o.optString(key, def) : def;
        } catch (Exception e) {
            return def;
        }
    }

    static void put(Context c, String key, String value) {
        try {
            File f = file(c);
            JSONObject o = f.exists()
                ? new JSONObject(new String(Files.readAllBytes(f.toPath()), StandardCharsets.UTF_8))
                : new JSONObject();
            o.put(key, value);
            Files.write(f.toPath(), o.toString(2).getBytes(StandardCharsets.UTF_8));
        } catch (Exception e) {
            Log.e("GTALock", "cannot save " + key, e);
        }
    }

    /** On-screen controls: 0 auto (hidden while a controller is used), 1 always, 2 off, 3 touch twin-stick. */
    static int touchMode(Context c) {
        String m = get(c, "touch_controls", "auto");
        if ("on".equals(m)) return 1;
        if ("off".equals(m)) return 2;
        if ("twinstick".equals(m)) return 3;
        return 0;
    }
}
