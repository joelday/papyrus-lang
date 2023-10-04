import { promisify } from 'util';
import winreg from 'winreg';

export function getLocalAppDataFolder() {
    return process.env.LOCALAPPDATA;
}
export function getHomeFolder() {
    if (process.platform === 'win32') {
        return process.env.USERPROFILE || process.env.HOMEPATH;
    }
    return process.env.HOME;
}
export function getUserName() {
    return process.env.USERNAME;
}
export function getTempFolder() {
    return process.env.TEMP;
}
export async function getRegistryValueData(key: string, value: string, hive: string = 'HKLM') {
    const reg = new winreg({
        hive,
        key,
    });
    try {
        const item = await promisify(reg.get).call(reg, value);
        return item.value;
    } catch (e) {
        /* empty */
    }
    return null;
}
