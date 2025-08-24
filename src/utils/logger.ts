import * as fs from 'fs'
import * as path from 'path'

const LOG_FILE_PATH = 'c:/tmp/tdd-guard.log'

export type LogType =
    | 'INPUT'
    | 'OUTPUT'
    | 'ERROR'
    | 'MODEL_REQUEST'
    | 'MODEL_RESPONSE'
    | 'MODEL_ERROR'

function ensureLogDirectory(): void {
    const logDir = path.dirname(LOG_FILE_PATH)
    if (!fs.existsSync(logDir)) {
        fs.mkdirSync(logDir, { recursive: true })
    }
}

export function logToFile(message: string, type: LogType): void {
    const timestamp = new Date().toISOString()
    const logEntry = `[${timestamp}] ${type}: ${message}\n`

    try {
        ensureLogDirectory()
        fs.appendFileSync(LOG_FILE_PATH, logEntry)
    } catch (error) {
        // Silently fail if we can't write to log file to avoid breaking the main functionality
        console.error(`Failed to write to log file: ${error}`)
    }
}
