import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;

public class FindCallers extends GhidraScript {
    public void run() throws Exception {
        String[] a = getScriptArgs();
        String name = a[0];
        SymbolTable st = currentProgram.getSymbolTable();
        SymbolIterator it = st.getSymbols(name);
        if (!it.hasNext()) { println("NOT FOUND: "+name); return; }
        Symbol s = it.next();
        Address addr = s.getAddress();
        ReferenceManager rm = currentProgram.getReferenceManager();
        FunctionManager fm = currentProgram.getFunctionManager();
        PrintWriter pw = new PrintWriter(new FileWriter("/tmp/callers.txt"));
        for (Reference ref : rm.getReferencesTo(addr)) {
            Address from = ref.getFromAddress();
            Function f = fm.getFunctionContaining(from);
            pw.println(from + " in " + (f!=null? f.getName() : "???"));
        }
        pw.close();
        println("wrote callers to /tmp/callers.txt");
    }
}
