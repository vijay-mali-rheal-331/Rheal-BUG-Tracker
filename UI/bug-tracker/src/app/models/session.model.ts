export type SessionStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';

export interface CreateSessionRequest {
  repoUrl?: string;
  branch?: string;
}

export interface CreateSessionResponse {
  sessionId: string;
  createdAt: string;
}

export interface SessionDto {
  sessionId: string;
  repoUrl?: string;
  branch?: string;
  status: SessionStatus;
  totalFiles: number;
  processedFiles: number;
  createdAt: string;
  errorMessage?: string;
  progressPercent: number;
}
