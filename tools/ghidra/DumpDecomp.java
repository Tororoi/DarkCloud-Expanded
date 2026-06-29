// Ghidra headless postScript: decompile a comma-separated list of function names.
//   analyzeHeadless ... -postScript DumpDecomp.java "Name1,Name2" /tmp/out.txt
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.util.task.ConsoleTaskMonitor;
import java.io.*;

public class DumpDecomp extends GhidraScript {
    public void run() throws Exception {
        String[] args = getScriptArgs();
        String[] names = (args.length > 0 && !args[0].isEmpty()) ? args[0].split(",") : new String[0];
        String outpath = args.length > 1 ? args[1] : "/tmp/ghidra_decomp.txt";
        FunctionManager fm = currentProgram.getFunctionManager();
        SymbolTable st = currentProgram.getSymbolTable();
        DecompInterface dec = new DecompInterface();
        dec.openProgram(currentProgram);
        ConsoleTaskMonitor mon = new ConsoleTaskMonitor();
        PrintWriter out = new PrintWriter(new FileWriter(outpath));
        for (String nm : names) {
            Function f = funcByName(fm, st, nm);
            if (f == null) { out.println("==== " + nm + " : NOT FOUND ===="); out.println(); continue; }
            DecompileResults res = dec.decompileFunction(f, 180, mon);
            String c = (res != null && res.decompileCompleted())
                ? res.getDecompiledFunction().getC()
                : "(decompile failed: " + (res != null ? res.getErrorMessage() : "null") + ")";
            out.println("==== " + nm + " @ " + f.getEntryPoint() + " ====");
            out.println(c);
            out.println();
        }
        out.close();
        println("wrote decomp for " + names.length + " names to " + outpath);
    }
    Function funcByName(FunctionManager fm, SymbolTable st, String nm) {
        for (Symbol s : st.getGlobalSymbols(nm)) {
            Function f = fm.getFunctionAt(s.getAddress());
            if (f != null) return f;
        }
        FunctionIterator it = fm.getFunctions(true);
        while (it.hasNext()) { Function f = it.next(); if (f.getName().equals(nm)) return f; }
        return null;
    }
}
