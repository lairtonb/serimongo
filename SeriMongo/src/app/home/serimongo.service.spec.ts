/* tslint:disable:no-unused-variable */

import { TestBed, async, inject } from '@angular/core/testing';
import { SerimongoService } from './serimongo.service';

describe('Service: Serimongo', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [SerimongoService]
    });
  });

  it('should ...', inject([SerimongoService], (service: SerimongoService) => {
    expect(service).toBeTruthy();
  }));
});
