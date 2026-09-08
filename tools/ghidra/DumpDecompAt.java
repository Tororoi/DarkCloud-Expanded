// Decompile the function(s) CONTAINING a comma-separated list of hex addresses.
//   -postScript DumpDecompAt.java "0x1db6a6c,0x1dadad0" /tmp/out.txt
import ghidra.app.decompiler.*;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import java.io.*;

public class DumpDecompAt extends GhidraScript {
    public void run() throws Exception {
        String[] a = getScriptArgs();
        String[] addrs = a[0].split(",");
        String out = a.length > 1 ? a[1] : "/tmp/decomp_at.txt";
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        PrintWriter pw = new PrintWriter(new FileWriter(out));
        for (String s : addrs) {
            Address addr = currentProgram.getAddressFactory().getAddress(s.trim());
            Function f = getFunctionContaining(addr);
            if (f == null) { pw.println("==== no function at " + s + " ===="); continue; }
            pw.println("==== " + f.getName() + " @ " + f.getEntryPoint() + " (contains " + s.trim() + ") ====");
            DecompileResults r = di.decompileFunction(f, 120, monitor);
            pw.println(r.getDecompiledFunction() != null ? r.getDecompiledFunction().getC() : "DECOMP FAILED");
        }
        pw.close();
        di.dispose();
        println("wrote " + out);
    }
}
