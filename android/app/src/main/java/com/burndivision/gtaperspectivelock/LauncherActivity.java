package com.burndivision.gtaperspectivelock;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Intent;
import android.content.res.AssetManager;
import android.database.Cursor;
import android.graphics.Color;
import android.graphics.Typeface;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.DocumentsContract;
import android.util.Log;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.TextView;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.Locale;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import org.json.JSONObject;

/**
 * First screen. Makes sure both data sets are in place, then starts the game:
 *  1. Carnage3D engine data (shaders, configs) - shipped inside the APK, extracted to internal storage.
 *  2. Original GTA 1 data (GTADATA) - NOT shipped. The player imports their own copy once,
 *     via the system folder picker, into the app's external files folder.
 */
public class LauncherActivity extends Activity {
    private static final String TAG = "GTALock";
    private static final int REQ_PICK_FOLDER = 42;
    // files that must exist for a usable GTA 1 install
    private static final String[] REQUIRED = { "STYLE001.G24", "NYC.CMP" };

    private TextView status;
    private Button pickButton;
    private Button playButton;
    private RadioGroup aspectGroup;
    private static final String[] ASPECTS = { "auto", "16:9", "4:3", "fill" };
    private static final String[] ASPECT_LABELS = { "Auto", "16:9", "4:3", "Fill" };
    private final Handler ui = new Handler(Looper.getMainLooper());

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        OrientationPolicy.apply(this);
        buildUi();
        new Thread(() -> {
            try {
                extractEngineAssets();
            } catch (IOException e) {
                Log.e(TAG, "asset extraction failed", e);
                setStatus("Failed to unpack engine data: " + e.getMessage());
                return;
            }
            ui.post(this::refresh);
        }).start();
    }

    private void buildUi() {
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER);
        root.setBackgroundColor(Color.rgb(16, 16, 20));
        int pad = (int) (24 * getResources().getDisplayMetrics().density);
        root.setPadding(pad, pad, pad, pad);

        TextView title = new TextView(this);
        title.setText("GTA Perspective Lock");
        title.setTextColor(Color.WHITE);
        title.setTextSize(26);
        title.setTypeface(Typeface.DEFAULT_BOLD);
        title.setGravity(Gravity.CENTER);
        root.addView(title);

        status = new TextView(this);
        status.setTextColor(Color.rgb(200, 200, 200));
        status.setTextSize(15);
        status.setGravity(Gravity.CENTER);
        status.setPadding(0, pad / 2, 0, pad / 2);
        status.setText("Preparing...");
        root.addView(status);

        pickButton = new Button(this);
        pickButton.setText("Import GTA data folder");
        pickButton.setVisibility(View.GONE);
        pickButton.setOnClickListener(v -> pickFolder());
        root.addView(pickButton);

        TextView aspectTitle = new TextView(this);
        aspectTitle.setText("View aspect ratio. Auto: 4:3 on square-ish screens (foldables unfolded), 16:9 on wide ones");
        aspectTitle.setTextColor(Color.rgb(200, 200, 200));
        aspectTitle.setGravity(Gravity.CENTER);
        root.addView(aspectTitle);

        aspectGroup = new RadioGroup(this);
        aspectGroup.setOrientation(RadioGroup.HORIZONTAL);
        aspectGroup.setGravity(Gravity.CENTER);
        for (int i = 0; i < ASPECTS.length; ++i) {
            RadioButton rb = new RadioButton(this);
            rb.setId(1000 + i);
            rb.setText(ASPECT_LABELS[i]);
            rb.setTextColor(Color.WHITE);
            aspectGroup.addView(rb);
        }
        aspectGroup.setOnCheckedChangeListener((g, id) -> {
            int i = id - 1000;
            if (i >= 0 && i < ASPECTS.length) writeAspect(ASPECTS[i]);
        });
        aspectGroup.setVisibility(View.GONE);
        root.addView(aspectGroup);

        playButton = new Button(this);
        playButton.setText("Play");
        playButton.setVisibility(View.GONE);
        playButton.setOnClickListener(v -> startGame());
        root.addView(playButton);

        setContentView(root);
    }

    private void setStatus(String s) {
        ui.post(() -> status.setText(s));
    }

    private File gtaDataDir() {
        return new File(getExternalFilesDir(null), "GTADATA");
    }

    private boolean hasGtaData() {
        File dir = gtaDataDir();
        for (String req : REQUIRED) {
            if (findIgnoreCase(dir, req) == null) return false;
        }
        return true;
    }

    private static File findIgnoreCase(File dir, String name) {
        String[] names = dir.list();
        if (names == null) return null;
        for (String n : names) if (n.equalsIgnoreCase(name)) return new File(dir, n);
        return null;
    }

    private void refresh() {
        if (hasGtaData()) {
            status.setText("GTA data found.\nThe player always faces up; the city turns around you.");
            pickButton.setText("Re-import GTA data folder");
            pickButton.setVisibility(View.VISIBLE);
            playButton.setVisibility(View.VISIBLE);
            showAspect();
            if (getIntent().getBooleanExtra("autostart", false) && !isFinishing()) startGame();
        } else {
            status.setText("This app needs your own copy of the original GTA (1997) PC data.\n\n"
                + "Copy the game's GTADATA folder to this device, tap the button below and choose it.\n"
                + "(Rockstar released the PC game as freeware in 2002.)");
            pickButton.setText("Import GTA data folder");
            pickButton.setVisibility(View.VISIBLE);
            playButton.setVisibility(View.GONE);
            aspectGroup.setVisibility(View.GONE);
        }
    }

    // ---------------------------------------------------------------- aspect ratio (r_aspect in the engine config)

    private File configFile() {
        return new File(getFilesDir(), "gamedata/config/sys_config.json");
    }

    private String readAspect() {
        try {
            JSONObject o = new JSONObject(new String(Files.readAllBytes(configFile().toPath()), StandardCharsets.UTF_8));
            return o.optString("r_aspect", "auto");
        } catch (Exception e) {
            return "auto";
        }
    }

    private void writeAspect(String value) {
        try {
            File f = configFile();
            JSONObject o = f.exists()
                ? new JSONObject(new String(Files.readAllBytes(f.toPath()), StandardCharsets.UTF_8))
                : new JSONObject();
            o.put("r_aspect", value);
            Files.write(f.toPath(), o.toString(2).getBytes(StandardCharsets.UTF_8));
        } catch (Exception e) {
            Log.e(TAG, "cannot save aspect ratio", e);
        }
    }

    private void showAspect() {
        String cur = readAspect();
        for (int i = 0; i < ASPECTS.length; ++i) {
            if (ASPECTS[i].equals(cur)) aspectGroup.check(1000 + i);
        }
        aspectGroup.setVisibility(View.VISIBLE);
    }

    private void startGame() {
        startActivity(new Intent(this, GameActivity.class));
        finish();
    }

    // ---------------------------------------------------------------- engine assets

    private void extractEngineAssets() throws IOException {
        File target = new File(getFilesDir(), "gamedata");
        File marker = new File(target, ".version");
        String want;
        try {
            // re-extract after every install/update
            want = String.valueOf(getPackageManager().getPackageInfo(getPackageName(), 0).lastUpdateTime);
        } catch (android.content.pm.PackageManager.NameNotFoundException e) {
            want = "0";
        }
        if (marker.exists() && want.equals(readSmall(marker))) return;
        String keepAspect = configFile().exists() ? readAspect() : null;
        setStatus("Unpacking engine data...");
        copyAssetDir(getAssets(), "gamedata", target);
        if (keepAspect != null) writeAspect(keepAspect);
        try (OutputStream o = new FileOutputStream(marker)) {
            o.write(want.getBytes());
        }
    }

    private static String readSmall(File f) {
        try (InputStream in = new java.io.FileInputStream(f)) {
            byte[] b = new byte[64];
            int n = in.read(b);
            return n > 0 ? new String(b, 0, n).trim() : "";
        } catch (IOException e) {
            return "";
        }
    }

    private static void copyAssetDir(AssetManager am, String path, File dst) throws IOException {
        String[] list = am.list(path);
        if (list == null || list.length == 0) {
            // a file
            dst.getParentFile().mkdirs();
            try (InputStream in = am.open(path); OutputStream out = new FileOutputStream(dst)) {
                pipe(in, out);
            }
            return;
        }
        dst.mkdirs();
        for (String child : list) copyAssetDir(am, path + "/" + child, new File(dst, child));
    }

    private static void pipe(InputStream in, OutputStream out) throws IOException {
        byte[] buf = new byte[1 << 16];
        int n;
        while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
    }

    // ---------------------------------------------------------------- GTA data import

    private void pickFolder() {
        Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        startActivityForResult(i, REQ_PICK_FOLDER);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQ_PICK_FOLDER || resultCode != RESULT_OK || data == null || data.getData() == null) return;
        final Uri tree = data.getData();
        pickButton.setEnabled(false);
        playButton.setVisibility(View.GONE);
        new Thread(() -> {
            try {
                importTree(tree);
                if (!hasGtaData()) {
                    setStatus("That folder doesn't look like GTADATA (STYLE001.G24 / NYC.CMP not found).\nPick the GTADATA folder itself, or the game folder that contains it.");
                } else {
                    setStatus("Import complete.");
                }
            } catch (Exception e) {
                Log.e(TAG, "import failed", e);
                setStatus("Import failed: " + e.getMessage());
            }
            ui.post(() -> {
                pickButton.setEnabled(true);
                refresh();
            });
        }).start();
    }

    private static final class Entry {
        String id, name, mime;
    }

    private java.util.List<Entry> children(Uri tree, String docId) {
        java.util.List<Entry> out = new java.util.ArrayList<>();
        Uri q = DocumentsContract.buildChildDocumentsUriUsingTree(tree, docId);
        ContentResolver cr = getContentResolver();
        try (Cursor c = cr.query(q, new String[] {
                DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                DocumentsContract.Document.COLUMN_MIME_TYPE }, null, null, null)) {
            while (c != null && c.moveToNext()) {
                Entry e = new Entry();
                e.id = c.getString(0);
                e.name = c.getString(1);
                e.mime = c.getString(2);
                out.add(e);
            }
        }
        return out;
    }

    private void importTree(Uri tree) throws IOException {
        String rootId = DocumentsContract.getTreeDocumentId(tree);
        // accept either GTADATA itself or a parent folder containing it
        String srcId = rootId;
        for (Entry e : children(tree, rootId)) {
            if (DocumentsContract.Document.MIME_TYPE_DIR.equals(e.mime) && e.name.equalsIgnoreCase("GTADATA")) {
                srcId = e.id;
                break;
            }
        }
        File dst = gtaDataDir();
        dst.mkdirs();
        int[] count = { 0 };
        copyTree(tree, srcId, dst, count);
    }

    private void copyTree(Uri tree, String docId, File dst, int[] count) throws IOException {
        for (Entry e : children(tree, docId)) {
            File out = new File(dst, e.name);
            if (DocumentsContract.Document.MIME_TYPE_DIR.equals(e.mime)) {
                out.mkdirs();
                copyTree(tree, e.id, out, count);
                continue;
            }
            count[0]++;
            setStatus(String.format(Locale.US, "Importing GTA data... %d files\n%s", count[0], e.name));
            Uri doc = DocumentsContract.buildDocumentUriUsingTree(tree, e.id);
            try (InputStream in = getContentResolver().openInputStream(doc); OutputStream o = new FileOutputStream(out)) {
                if (in == null) throw new IOException("cannot open " + e.name);
                pipe(in, o);
            }
        }
    }
}
