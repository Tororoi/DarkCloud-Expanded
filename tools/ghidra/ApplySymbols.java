// Ghidra headless postScript for the dun.bin RAW program (base 0x1DABC80):
// disassemble + name functions at every symbols.txt vaddr inside this program's memory.
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.SourceType;
import ghidra.program.model.mem.Memory;
import java.io.*;

public class ApplySymbols extends GhidraScript {
    public void run() throws Exception {
        String symfile = "/Users/thomascantwell/DarkCloud-Enhanced/tools/ghidra/symbols.txt";
        FunctionManager fm = currentProgram.getFunctionManager();
        AddressSpace sp = currentProgram.getAddressFactory().getDefaultAddressSpace();
        Memory mem = currentProgram.getMemory();
        BufferedReader br = new BufferedReader(new FileReader(symfile));
        String line; int created = 0, named = 0;
        while ((line = br.readLine()) != null) {
            String[] p = line.trim().split("\\s+");
            if (p.length < 2) continue;
            long va = Long.parseLong(p[0].replace("0x", ""), 16);
            Address addr = sp.getAddress(va);
            if (!mem.contains(addr)) continue;
            try { disassemble(addr); } catch (Exception e) {}
            Function f = fm.getFunctionAt(addr);
            if (f == null) { try { createFunction(addr, p[1]); created++; } catch (Exception e) {} }
            else { try { f.setName(p[1], SourceType.USER_DEFINED); named++; } catch (Exception e) {} }
        }
        br.close();
        println("ApplySymbols: created " + created + ", renamed " + named);
    }
}
