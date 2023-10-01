import { Parser } from 'binary-parser';
import * as fs from 'fs';
import { PapyrusGame } from '../PapyrusGame';

// This interface contains the same members that are in the "Header" class in F:\workspace\skyrim-mod-workspace\Champollion\Pex\Header.hpp
// The members are in the same order as they are in the C++ header file.
export interface PexHeader {
    /**
     * Determines the game that the pex file was compiled for.
     * This is determined by the endianness of the magic number.
     * If the magic number is 0xFA57C0DE, then the game is Fallout 4.
     * If the magic number is 0xDEC057FA, then the game is Skyrim.
     */
    Game: PapyrusGame;
    /**
     * The major version game that the pex file was compiled for.
     */
    MajorVersion: number;
    /**
     * The minor version game that the pex file was compiled for.
     */
    MinorVersion: number;
    /**
     * The game ID of the game that the pex file was compiled for.
     */
    GameID: number;
    /**
     * The timestamp of when the pex file was compiled.
     */
    CompileTime: number;
    /**
     * The name of the source file that was compiled to create the pex file.
     * This can either be an absolute path (on the *ORIGINAL MACHINE* that it was compiled on)
     * or a relative path.
     */
    SourceFileName: string;
    /**
     * The name of the user that compiled the pex file.
     * This is the name of the user on the *ORIGINAL MACHINE* that it was compiled on.
     */
    UserName: string;
    /**
     * The name of the computer that compiled the pex file.
     */
    ComputerName: string;
}

export interface PexBinary {
  Path: string;
  Header: PexHeader;
  DebugInfo: DebugInfo;
  // TODO: add the rest of the data
}

export enum FunctionType{
  Method,
  Getter,
  Setter
}
// names these are all indexed into the string table, but copied into the interface
export interface FunctionInfo {
  ObjectName: string;
  StateName: string;
  FunctionName: string;
  FunctionType: FunctionType;
  LineNumbers: number[];
}

export interface PropertyGroup {
  ObjectName: string,
  GroupName: string,
  DocString: string,
  UserFlags: number,
  Names: string[]
}

export interface StructOrder {
  StructName: string,
  OrderName: string,
  Names: string[]
}

export interface DebugInfo {
  ModificationTime: number,
  FunctionInfos: FunctionInfo[],
  PropertyGroups: PropertyGroup[],
  /**
   * Fallout 4 only
   */
  StructOrders?: StructOrder[]
}

// TODO: maybe implement this
class PexIndexedString {
  public readonly index: number;
  public readonly str: string;
  constructor(index: number, str: string) {
      this.index = index;
      this.str = str;
  }

  public toString(): string {
      return this.str;
  }
}

export class PexStringTable {
  public readonly strings: string[];
  constructor(strings: string[]) {
      this.strings = strings;
  }
}

const LE_MAGIC_NUMBER = 0xfa57c0de; // 4200055006, values when read little endian
const BE_MAGIC_NUMBER = 0xdec057fa; // 3737147386, values when read little endian


function DetermineEndianness(buffer: Buffer) {
  const magicNumber = buffer.readUInt32LE(0);
  return DetermineEndiannessFromNumber(magicNumber);
}

function DetermineEndiannessFromNumber(number: number) {
  if (number === LE_MAGIC_NUMBER) {
      return 'little';
  } else if (number === BE_MAGIC_NUMBER) {
      return 'big';
  } else {
      return undefined;
  }
}

function getGameFromEndianness(endianness: 'little' | 'big') {
    if (endianness === 'little') {
        return PapyrusGame.fallout4;
    }
    return PapyrusGame.skyrimSpecialEdition;
}

/**
 * Parses a pex file.
 * 
 * NOTE: This only currently implements the parsing of the header and the debug info.
 */
export class PexReader {
    public readonly path;
    
    public readonly game: PapyrusGame = PapyrusGame.skyrimSpecialEdition;
    constructor(path: string) {
        this.path = path;
    }
    private endianness: "little" | "big" = "little";
    private stringTable: PexStringTable = new PexStringTable([]);

  // constants
  
  // parsers
  private readonly StringParser = () =>
      new Parser()
          .uint16('__strlen')
          .string('__string', { length: '__strlen', encoding: 'ascii', zeroTerminated: false });
  
  private readonly _strNest = { type: this.StringParser(), formatter: (x:any): string => x.__string };
  
  private readonly StringTableParser = 
      new Parser().uint16('__tbllen').array('__strings', {
          type: this.StringParser(),
          formatter: (x:any): string[] => x.map((y:any) => y.__string),
          length: '__tbllen',
      });
  
  private readonly _strTableNest = { type: this.StringTableParser , formatter: (x:any): 
    PexStringTable => {
      // TODO: Global state hack to get around not being able to reference the parsed string table in the middle of the parse
      this.stringTable = new PexStringTable(x.__strings);
      return this.stringTable;
    }
  };
  
  private readonly FunctionInfoRawParser = () => new Parser()
    .uint16('ObjectName')
    .uint16('StateName')
    .uint16('FunctionName')
    .uint8('FunctionType')
    .uint16('LineNumbersCount')
    .array('LineNumbers', {
      type: this.GetUintType(),
      length: 'LineNumbersCount',
      formatter: (x:any): number[] => x.map((y:any) => y.__val)
    })
  
  
  private readonly FunctionInfosParser = () => new Parser().uint16('__FIlen').array('__infos', {
    type: this.FunctionInfoRawParser(),
    length: '__FIlen',
    formatter: (x:any): FunctionInfo[] => x.map((y:any) => {
      let functinfo = {
        ObjectName: this.TableLookup(y.ObjectName),
        StateName: this.TableLookup(y.StateName),
        FunctionName: this.TableLookup(y.FunctionName),
        FunctionType: y.FunctionType as FunctionType,
        LineNumbers: y.LineNumbers
      } as FunctionInfo;
      return functinfo;
    }),
  });
  public GetEndianness() {
    return this.endianness;
  }
  private GetUintType(){
    return new Parser().uint16("__val");
  }
  private readonly PropertyGroupRawParser = () => new Parser()
    .uint16('ObjectName')
    .uint16('GroupName')
    .uint16('DocString')
    .uint32('UserFlags')
    .uint16('NamesCount')
    .array('Names', {
      type: this.GetUintType(),
      length: 'NamesCount',
      formatter: (x:any): number[] => x.map((y:any) => y.__val)
    })
  private TableLookup (x: number){
    if (x >= this.stringTable.strings.length){
      return "<NONE>";
    }
    return this.stringTable.strings[x];
  }
  private readonly PropertyGroupsParser = () => new Parser().uint16('__PGlen').array('__infos', {
    type: this.PropertyGroupRawParser(),
    length: '__PGlen',
    formatter: (x:any): PropertyGroup[] => x.map((y:any) => {
      let pgroups =  {
        ObjectName: this.TableLookup(y.ObjectName),
        GroupName: this.TableLookup(y.GroupName),
        DocString: this.TableLookup(y.DocString),
        UserFlags: y.UserFlags,
        Names: y.Names.map((z:any) => this.TableLookup(z))
      } as PropertyGroup;
      return pgroups;
    })
  });

  private readonly StructOrderRawParser = () => new Parser()
    .uint16('StructName')
    .uint16('OrderName')
    .uint16('NamesCount')
    .array('Names', {
      type: this.GetUintType(),
      length: 'NamesCount',
      formatter: (x:any): number[] => x.map((y:any) => y.__val)

    })

  private readonly StructOrdersParser = () => new Parser().uint16('__SOlen').array('__infos', {
    type: this.StructOrderRawParser(),
    length: '__SOlen',
    formatter: (x:any): StructOrder[] => x.map((y:any) => {
      let sorders = {
        StructName: this.TableLookup(y.StructName),
        OrderName: this.TableLookup(y.OrderName),
        Names: y.Names.map((z:any) => this.TableLookup(z))
      } as StructOrder;
      return sorders;
    })
  });

  private readonly _doParseDebugInfo = () => {
    return new Parser()
      .uint64('ModificationTime')
      .nest('FunctionInfos', {
        type: this.FunctionInfosParser(),
        formatter: (x:any): FunctionInfo[] => x.__infos
      })
      .nest('PropertyGroups', {
        type: this.PropertyGroupsParser(),
        formatter: (x:any): PropertyGroup[] => x.__infos
      })
      .choice("StructOrders", {
        tag: () => {
          let val =this.endianness === "little" ? 1 : 0
          return val;
        },
        choices: { 
          0: new Parser().skip(0),
          1: this.StructOrdersParser()
        },
        formatter: (x:any): StructOrder[] | undefined => {
          if (this.endianness === "little" && x){
            return x.__infos;
          }
          return undefined;
        }
      })
    }
  

  private readonly ParseDebugInfo = () => new Parser()
          .uint8('HasDebugInfo')
          .choice('DebugInfo', {
              tag: 'HasDebugInfo',
              choices: {
                  0: new Parser().skip(0),
                  1: this._doParseDebugInfo()
              },
              formatter: (x:any): DebugInfo | undefined => {
                  if (!x) {
                      return undefined;
                  }
                  return x;
              }
          })
  
  private readonly _debugInfoNest = { type: this.ParseDebugInfo(), formatter: (x:any): DebugInfo | undefined => x ? x.DebugInfo : undefined };
  
  private readonly HeaderParser = () =>
      new Parser()
          .uint32('MagicNumber')
          .uint8('MajorVersion')
          .uint8('MinorVersion')
          .uint16('GameID')
          .uint64('CompileTime')
          .nest('SourceFileName', this._strNest)
          .nest('UserName', this._strNest)
          .nest('ComputerName',this. _strNest);

  private readonly _HeaderNest = (endianness: 'little' | 'big') => {
      return {
          type: this.HeaderParser(),
          formatter: (x: any): PexHeader => {
              return {
                  Game: getGameFromEndianness(endianness),
                  MajorVersion: x.MajorVersion,
                  MinorVersion: x.MinorVersion,
                  GameID: x.GameID,
                  CompileTime: x.CompileTime,
                  SourceFileName: x.SourceFileName,
                  UserName: x.UserName,
                  ComputerName: x.ComputerName,
              };
          },
      };
  };

  
  private ReadPexBinary(buffer: Buffer): PexBinary | undefined {
      let endianness: 'little' | 'big' | undefined = DetermineEndianness(buffer);
      if (!endianness) {
          return undefined;
      }
      const Pex = new Parser()
                          .endianess(endianness)
                          .nest('Header', this._HeaderNest(endianness))
                          .nest('StringTable',this._strTableNest)
                          .nest('DebugInfo', this._debugInfoNest)
                          .parse(buffer);
  
      return {
        Path: this.path,
        Header: Pex.Header,
        DebugInfo: Pex.DebugInfo
      }
  }

  private ReadHeader(buffer: Buffer) {

    return new Parser()
                .endianess(this.endianness)
                .nest('Header', this._HeaderNest(this.endianness)).parse(buffer);
  }

  public async ReadPexHeader(): Promise<PexHeader | undefined> {
      // read the binary file from the path into a byte buffer
      if (!fs.existsSync(this.path) || !fs.lstatSync(this.path).isFile()) {
        return undefined;
      }

      const buffer = fs.readFileSync(this.path);
      if (!buffer || buffer.length < 4) {
          return undefined;
      }
      let endianness: 'little' | 'big' | undefined = DetermineEndianness(buffer);
      if (!endianness) {
          return undefined;
      }
      this.endianness = endianness;
  
      return this.ReadHeader(buffer);
  }
  // not complete
  async ReadPex(): Promise<PexBinary | undefined>{
    const buffer = fs.readFileSync(this.path);
    if (!buffer || buffer.length < 4) {
      return undefined;
    }
    let endianness: 'little' | 'big' | undefined = DetermineEndianness(buffer);
    if (!endianness) {
        return undefined;
    }
    this.endianness = endianness;
    return this.ReadPexBinary(buffer);
  }
}

// returns the 64-bit timestamp of when the pex file was compiled
// if file not found or not parsable, returns -1
export async function GetCompiledTime(path: string): Promise<number> {
    const pex = new PexReader(path);
    const header = await pex.ReadPexHeader();
    if (!header) {
        return -1;
    }
    return header.CompileTime;
}

// // Test the PexReader
// let pexreader = new PexReader(
//     'F:\\workspace\\skyrim-mod-workspace\\papyrus-lang\\src\\papyrus-lang-vscode\\_wetbpautoadjust.pex'
// );


// pexreader.ReadPex().then((pex) => {
//   console.log(pex);
//   console.log('done');
// });
