﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsmResolver.Net.Signatures;
using AsmResolver.X86;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AsmResolver.Tests.Native
{
    [TestClass]
    public class X86AssemblerTests
    {

        [TestMethod]
        public void RegOrMem8_Reg8()
        {
            var body = CreateRegOrMemTestInstructions(X86OpCodes.Add_RegOrMem8_Reg8, X86Mnemonic.Add, false).ToArray();

            TestAssembler(body);
        }

        [TestMethod]
        public void Reg8_RegOrMem8()
        {
            var body = CreateRegOrMemTestInstructions(X86OpCodes.Add_Reg8_RegOrMem8, X86Mnemonic.Add, true).ToArray();

            TestAssembler(body);
        }

        [TestMethod]
        public void RegOrMem8_Reg8_SIB()
        {
            
        }

        private static void TestAssembler(IReadOnlyList<X86Instruction> instructions)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "test.bin");
            using (var stream = File.Create(path))
            {
                var writer = new BinaryStreamWriter(stream);
                var assembler = new X86Assembler(writer);

                foreach (var instruction in instructions)
                    assembler.Write(instruction);
            }

            ValidateCode(instructions, File.ReadAllBytes(path));
        }

        private static IEnumerable<X86Instruction> CreateRegOrMemTestInstructions(X86OpCode opcode, X86Mnemonic mnemonic, bool flippedOperands)
        {  
            for (int operandType = 0; operandType < 3; operandType++)
            {
                for (int register2Index = 0; register2Index < 8; register2Index++)
                {
                    for (int register1Index = 0; register1Index < 8; register1Index++)
                    {
                        var operand1 = new X86Operand((X86Register)register1Index | X86Register.Eax,
                                X86OperandType.BytePointer);
                        var operand2 = new X86Operand((X86Register)register2Index, X86OperandType.Normal);

                        var instruction = new X86Instruction()
                        {
                            OpCode = opcode,
                            Mnemonic = mnemonic,
                        };

                        if (flippedOperands)
                        {
                            instruction.Operand2 = operand1;
                            instruction.Operand1 = operand2;
                        }
                        else
                        {
                            instruction.Operand1 = operand1;
                            instruction.Operand2 = operand2;
                        }

                        switch (register1Index)
                        {
                            case 4: // esp
                                continue;
                            case 5: // ebp
                                if (operandType != 0)
                                    continue;
                                operand1.Value = 0x1337u;
                                break;
                        }

                        switch (operandType)
                        {
                            case 1:
                                operand1.Correction = (sbyte)1;
                                break;
                            case 2:
                                operand1.Correction = 0x1337;
                                break;
                        }
                        yield return instruction;
                    }
                }
            }
        }


        private static void ValidateCode(IReadOnlyList<X86Instruction> originalBody, byte[] assemblerOutput)
        {
            var formatter = new FasmX86Formatter();
            var reader = new MemoryStreamReader(assemblerOutput);
            var disassembler = new X86Disassembler(reader);

            for (int i = 0; i < originalBody.Count; i++)
            {
                var newInstruction = disassembler.ReadNextInstruction();
                Assert.AreEqual(formatter.FormatInstruction(originalBody[i]),
                    formatter.FormatInstruction(newInstruction));
            }
        }

        
    }
}
