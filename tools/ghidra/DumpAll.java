// Decompile EVERY function in the program to one file (headers "==== name @ addr ====") for grepping.
//   -postScript DumpAll.java /tmp/all.txt
import ghidra.app.decompiler.*;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.*;
import java.io.*;

public class DumpAll extends GhidraScript {
    public void run() throws Exception {
        String[] a = getScriptArgs();
        String out = a.length > 0 ? a[0] : "/tmp/decomp_all.txt";
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        PrintWriter pw = new PrintWriter(new FileWriter(out));
        int n = 0;
        for (Function f : currentProgram.getFunctionManager().getFunctions(true)) {
            if (monitor.isCancelled()) break;
            pw.println("==== " + f.getName() + " @ " + f.getEntryPoint() + " ====");
            DecompileResults r = di.decompileFunction(f, 60, monitor);
            pw.println(r.getDecompiledFunction() != null ? r.getDecompiledFunction().getC() : "DECOMP FAILED");
            n++; pw.flush();
        }
        pw.close();
        di.dispose();
        println("wrote " + n + " functions to " + out);
    }
}
