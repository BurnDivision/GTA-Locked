// Ghidra headless post-script: locate camera / render code in GTA 1 (2002 Win build).
// Writes a report listing functions that reference camera/render-related strings,
// plus their decompiled C.
// Usage: analyzeHeadless <proj> gta -process gta.exe -noanalysis -scriptPath re/scripts \
//        -postScript CameraRecon.java /abs/path/camera_recon.txt
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;
import java.util.*;

public class CameraRecon extends GhidraScript {
    String[] KEYS = {"zoom", "ddraw", "directdraw", "camera", "rotat", "angle", "view", "screen", "block", "persp", ".drv", "mgl"};

    @Override
    public void run() throws Exception {
        String out = getScriptArgs().length > 0 ? getScriptArgs()[0] : "camera_recon.txt";
        PrintWriter pw = new PrintWriter(new FileWriter(out));
        Listing listing = currentProgram.getListing();
        ReferenceManager rm = currentProgram.getReferenceManager();
        Map<Function, Set<String>> hits = new LinkedHashMap<>();
        pw.println("== Strings of interest and referencing functions ==");
        for (Data d : listing.getDefinedData(true)) {
            if (!d.hasStringValue()) continue;
            Object v = d.getValue();
            if (v == null) continue;
            String s = v.toString();
            String sl = s.toLowerCase();
            boolean m = false;
            for (String k : KEYS) if (sl.contains(k)) { m = true; break; }
            if (!m || s.length() > 120) continue;
            StringBuilder fns = new StringBuilder();
            for (Reference r : rm.getReferencesTo(d.getAddress())) {
                Function f = getFunctionContaining(r.getFromAddress());
                if (f != null) {
                    fns.append(f.getName()).append("@").append(f.getEntryPoint()).append(" ");
                    hits.computeIfAbsent(f, x -> new TreeSet<>()).add(s);
                }
            }
            pw.printf("%s  \"%s\"  <- %s%n", d.getAddress(), s.replace("\n", "\\n"), fns);
        }
        pw.println();
        int n = 0; for (Function f : listing.getFunctions(true)) n++;
        pw.println("total functions: " + n);
        pw.println();
        pw.println("== Decompiled functions referencing those strings ==");
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        for (Map.Entry<Function, Set<String>> e : hits.entrySet()) {
            Function f = e.getKey();
            pw.println("------------------------------------------------------------");
            pw.println(f.getName() + " @ " + f.getEntryPoint() + "  size=" + f.getBody().getNumAddresses() + "  strings=" + e.getValue());
            DecompileResults res = di.decompileFunction(f, 60, monitor);
            if (res != null && res.decompileCompleted()) {
                String c = res.getDecompiledFunction().getC();
                if (c.length() > 12000) c = c.substring(0, 12000) + "\n...[truncated]";
                pw.println(c);
            }
        }
        pw.close();
        println("Wrote " + out);
    }
}
