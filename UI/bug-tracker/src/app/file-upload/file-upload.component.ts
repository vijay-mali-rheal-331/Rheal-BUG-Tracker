import { Component } from '@angular/core';
import { HttpEventType } from '@angular/common/http';
import { UploadService } from '../services/upload.service';

@Component({
  selector: 'app-file-upload',
  standalone: false,
  templateUrl: './file-upload.component.html',
  styleUrl: './file-upload.component.scss'
})
export class FileUploadComponent {
  selectedFiles: File[] = [];
  status: 'idle' | 'uploading' | 'success' | 'error' = 'idle';
  progress = 0;
  errorMessage = '';

  constructor(private uploadService: UploadService) {}

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files) {
      const incoming = Array.from(input.files);
      incoming.forEach(f => {
        if (!this.selectedFiles.find(x => x.name === f.name && x.size === f.size)) {
          this.selectedFiles.push(f);
        }
      });
    }
    input.value = '';
  }

  onFolderSelected(event: Event): void {
    this.onFilesSelected(event);
  }

  removeFile(index: number): void {
    this.selectedFiles.splice(index, 1);
  }

  clearAll(): void {
    this.selectedFiles = [];
    this.status = 'idle';
    this.progress = 0;
  }

  send(): void {
    if (!this.selectedFiles.length) return;
    this.status = 'uploading';
    this.progress = 0;
    this.errorMessage = '';

    this.uploadService.upload(this.selectedFiles).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          this.progress = Math.round((100 * event.loaded) / event.total);
        } else if (event.type === HttpEventType.Response) {
          this.status = 'success';
        }
      },
      error: err => {
        this.status = 'error';
        this.errorMessage = err?.message || 'Upload failed';
      }
    });
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
