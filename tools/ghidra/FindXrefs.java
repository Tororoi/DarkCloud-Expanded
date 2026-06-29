// Ghidra headless postScript: find all references TO a comma-separated list of hex addresses.
//   -postScript FindXrefs.java "0x1dc4494,0x1dc44f0" /tmp/xrefs.txt
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;

public class FindXrefs extends GhidraScript {
    public void run() throws Exception {
        String[] args = getScriptArgs();
        String[] addrs = (args.length > 0 && !args[0].isEmpty()) ? args[0].split(",") : new String[0];
        String outpath = args.length > 1 ? args[1] : "/tmp/ghidra_xrefs.txt";
        ReferenceManager rm = currentProgram.getReferenceManager();
        FunctionManager fm = currentProgram.getFunctionManager();
        PrintWriter out = new PrintWriter(new FileWriter(outpath));
        for (String a : addrs) {
            Address target = currentProgram.getAddressFactory().getAddress(a.trim());
            out.println("==== refs to " + a.trim() + " ====");
            ReferenceIterator it = rm.getReferencesTo(target);
            int n = 0;
            while (it.hasNext()) {
                Reference r = it.next();
                Address from = r.getFromAddress();
                Function f = fm.getFunctionContaining(from);
                String fn = (f != null) ? f.getName() + "+" + (from.getOffset() - f.getEntryPoint().getOffset()) : "(no func)";
                out.println("  " + from + "  " + r.getReferenceType() + "  " + fn);
                n++;
            }
            if (n == 0) out.println("  (none)");
            out.println();
        }
        out.close();
        println("wrote xrefs for " + addrs.length + " addrs to " + outpath);
    }
}
