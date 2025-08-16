import { execFileSync } from 'child_process'
import { join } from 'path'
import { existsSync, mkdirSync } from 'fs'
import { IModelClient } from '../../contracts/types/ModelClient'
import { Config } from '../../config/Config'

export class ClaudeCli implements IModelClient {
  private readonly config: Config

  constructor(config?: Config) {
    this.config = config ?? new Config()
  }

  async ask(prompt: string): Promise<string> {
    const claudeBinary = this.config.useSystemClaude
      ? 'claude'
      : join(process.cwd(), 'node_modules', '.bin', 'claude.cmd')

    const args = [
      '-',
      '--output-format',
      'json',
      '--max-turns',
      '5',
      '--model',
      'sonnet',
    ]
    const claudeDir = join(process.cwd(), '.claude')

    if (!existsSync(claudeDir)) {
      mkdirSync(claudeDir, { recursive: true })
    }

    const output = execFileSync(claudeBinary, args, {
      encoding: 'utf-8',
      timeout: 60000,
      input: prompt,
      shell: true,
      cwd: claudeDir,
    })

    // Parse the Claude CLI response and extract the result field
    const response = JSON.parse(output)

    return response.result
  }
}
