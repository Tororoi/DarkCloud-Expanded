// Dump instructions (addr, bytes, mnemonic) for an ADDRESS RANGE. Args: "<startHex>" "<endHex>" "<outfile>"
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.*;
import java.io.*;

public class DumpListingAt extends GhidraScript {
    public void run() throws Exception {
        String[] a = getScriptArgs();
        Address start = toAddr(Long.parseLong(a[0].replace("0x",""), 16));
        Address end = toAddr(Long.parseLong(a[1].replace("0x",""), 16));
        String out = a.length > 2 ? a[2] : "/tmp/listing_at.txt";
        PrintWriter pw = new PrintWriter(new FileWriter(out));
        Listing lst = currentProgram.getListing();
        for (Address p = start; p.compareTo(end) < 0; ) {
            Instruction in = lst.getInstructionAt(p);
            if (in == null) { in = lst.getInstructionContaining(p); if (in == null) { disassemble(p); in = lst.getInstructionAt(p); } }
            if (in == null) { pw.println(String.format("%08x  ????", p.getOffset())); p = p.add(4); continue; }
            byte[] b = in.getBytes(); StringBuilder hx = new StringBuilder();
            for (byte x : b) hx.append(String.format("%02x", x));
            pw.println(String.format("%08x  %-8s  %s", in.getAddress().getOffset(), hx.toString(), in.toString()));
            p = in.getAddress().add(in.getLength());
        }
        pw.close();
        println("wrote listing to " + out);
    }
}
