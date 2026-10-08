import { WebUIElement, attr, observable } from '@microsoft/webui-framework';

interface Task {
  id: string;
  title: string;
  done: boolean;
}

export class TaskBoard extends WebUIElement {
  @attr heading = '';
  @observable items: Task[] = [];
  @observable pendingCount = 0;

  newTitle!: HTMLInputElement;
  private nextId = 1000;

  onToggle(id: string): void {
    this.items = this.items.map(t => (t.id === id ? { ...t, done: !t.done } : t));
    this.updatePending();
  }

  onAdd(e: SubmitEvent): void {
    e.preventDefault();
    const title = this.newTitle.value.trim();
    if (!title) return;
    this.items = [...this.items, { id: String(this.nextId++), title, done: false }];
    this.newTitle.value = '';
    this.updatePending();
  }

  private updatePending(): void {
    this.pendingCount = this.items.filter(t => !t.done).length;
  }
}

TaskBoard.define('task-board');
