import * as ini from 'ini';
import * as fs from "fs";
import { promisify } from 'util';
const readFile = promisify(fs.readFile);


export interface INIData {
  [key: string]: any;
}

export function ParseIniArray(data: INIData): INIData[] | undefined {
  if (!data || data.size === undefined || data.size === null) {
    return undefined;
  }
  let array = new Array<INIData>();
  if (data.size === 0) {
    return array;
  }
  for (let i = 0; i < data.size; i++) {
    array.push({} as INIData);
  }
  // Keys in INI arrays are in the format of 1\{key1}, 1\{key2}, 2\{key1}, 2\{key2}, etc.
  let keys = Object.keys(data);
  keys.forEach((key) => {
    if (key !== 'size') {
      const parts = key.split('\\');
      if (parts.length === 2) {
        // INI arrays are 1-indexed
        const index = parseInt(parts[0], 10) - 1;
        const subKey = parts[1];
        array[index][subKey] = data[key];
      }
    }
  });
  return array;
}

export function SerializeIniArray(data: INIData[]): INIData {
  let iniData = {} as INIData;
  iniData.size = data.length;
  data.forEach((value, index) => {
    Object.keys(value).forEach((key) => {
      iniData[`${index + 1}\\${key}`] = value[key];
    });
  });
  return iniData;
}

export async function ParseIniFile(IniPath: string): Promise<INIData | undefined> {
  if (!fs.existsSync(IniPath) || !fs.lstatSync(IniPath).isFile()) {
    return undefined;
  }
  let IniText = await readFile(IniPath, 'utf-8');
  if (!IniText) {
    return undefined;
  }
  return ini.parse(IniText) as INIData;
}

export async function WriteChangesToIni(gameIniPath: string, skyrimIni: INIData) {
  const file = fs.openSync(gameIniPath, 'w');
  if (!file) {
    return false;
  }
  fs.writeFileSync(file, ini.stringify(skyrimIni));
  return false;
}
