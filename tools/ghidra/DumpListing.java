// Dump instructions (addr, bytes, mnemonic) for a function by name. Args: "<funcName>" "<outfile>"
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import java.io.*;

public class DumpListing extends GhidraScript {
    public void run() throws Exception {
        String[] a = getScriptArgs();
        String name = a[0]; String out = a.length>1 ? a[1] : "/tmp/listing.txt";
        PrintWriter pw = new PrintWriter(new FileWriter(out));
        SymbolTable st = currentProgram.getSymbolTable();
        SymbolIterator it = st.getSymbols(name);
        if (!it.hasNext()) { pw.println("NOT FOUND: "+name); pw.close(); return; }
        Symbol s = it.next();
        Function f = getFunctionAt(s.getAddress());
        Address end = (f!=null) ? f.getBody().getMaxAddress() : s.getAddress().add(0x600);
        Listing lst = currentProgram.getListing();
        InstructionIterator ii = lst.getInstructions(s.getAddress(), true);
        while (ii.hasNext()) {
            Instruction in = ii.next();
            if (in.getAddress().compareTo(end) > 0) break;
            byte[] b = in.getBytes();
            StringBuilder hx = new StringBuilder();
            for (byte x : b) hx.append(String.format("%02x", x));
            pw.println(String.format("%08x  %-8s  %s", in.getAddress().getOffset(), hx.toString(), in.toString()));
        }
        pw.close();
        println("wrote listing to "+out);
    }
}
